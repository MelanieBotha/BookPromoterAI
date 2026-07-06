using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace BookPromoterAI;

/// <summary>On-device text-to-speech (no ElevenLabs or other cloud TTS). Uses espeak-ng on Linux.</summary>
class LocalSpeechService
{
    static readonly Regex SafeText = new(@"[^\p{L}\p{N}\p{P}\p{Z}]", RegexOptions.Compiled);
    string? _espeakPath;

    public bool IsAvailable => EspeakPath() is not null;

    public async Task<(byte[]? Wav, double DurationMs, string? Error)> SynthesizeAsync(
        string text, CancellationToken cancellationToken = default)
    {
        var cleaned = Sanitize(text);
        if (string.IsNullOrWhiteSpace(cleaned))
            return (null, 0, "Enter some text to read aloud.");

        if (OperatingSystem.IsWindows())
        {
            var windows = await TryWindowsSapiAsync(cleaned, cancellationToken);
            if (windows.Wav is not null) return windows;
        }

        return await SynthesizeEspeakAsync(cleaned, cancellationToken);
    }

    static string Sanitize(string text)
    {
        var limited = ReadAloudScript.LimitWords(text.Trim());
        var stripped = SafeText.Replace(limited, " ");
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    static async Task<(byte[]? Wav, double DurationMs, string? Error)> TryWindowsSapiAsync(
        string text, CancellationToken cancellationToken)
    {
        var tempWav = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.wav");
        var tempTxt = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.txt");
        var tempPs1 = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.ps1");
        try
        {
            await File.WriteAllTextAsync(tempTxt, text, Encoding.UTF8, cancellationToken);
            var script = $$"""
                Add-Type -AssemblyName System.Speech
                $s = New-Object System.Speech.Synthesis.SpeechSynthesizer
                $s.Rate = 0
                $s.SetOutputToWaveFile('{{tempWav.Replace("'", "''")}}')
                $s.Speak((Get-Content -Raw -Encoding UTF8 '{{tempTxt.Replace("'", "''")}}'))
                $s.Dispose()
                """;
            await File.WriteAllTextAsync(tempPs1, script, cancellationToken);
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File {ProcessTools.QuoteArg(tempPs1)}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return (null, 0, null);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(tempWav))
                return (null, 0, null);
            var bytes = await File.ReadAllBytesAsync(tempWav, cancellationToken);
            return (bytes, WavDurationMs(bytes), null);
        }
        catch
        {
            return (null, 0, null);
        }
        finally
        {
            TryDelete(tempWav);
            TryDelete(tempTxt);
            TryDelete(tempPs1);
        }
    }

    async Task<(byte[]? Wav, double DurationMs, string? Error)> SynthesizeEspeakAsync(
        string text, CancellationToken cancellationToken)
    {
        var espeak = EspeakPath();
        if (espeak is null)
            return (null, 0, "Read-aloud is not available on this server yet. Try the Promo video style instead.");

        var tempWav = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.wav");
        var tempTxt = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempTxt, text, Encoding.UTF8, cancellationToken);
            var psi = new ProcessStartInfo
            {
                FileName = espeak,
                Arguments = $"-v en-us -s 165 -w {ProcessTools.QuoteArg(tempWav)} -f {ProcessTools.QuoteArg(tempTxt)}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return (null, 0, "Could not start the speech engine.");
            var err = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(tempWav))
                return (null, 0, string.IsNullOrWhiteSpace(err) ? "Speech synthesis failed." : err.Trim());

            var bytes = await File.ReadAllBytesAsync(tempWav, cancellationToken);
            return (bytes, WavDurationMs(bytes), null);
        }
        finally
        {
            TryDelete(tempWav);
            TryDelete(tempTxt);
        }
    }

    string? EspeakPath() => _espeakPath ??= ProcessTools.FindExecutable(
        "/usr/bin/espeak-ng",
        "/usr/bin/espeak",
        "espeak-ng",
        "espeak");

    static double WavDurationMs(byte[] wav)
    {
        if (wav.Length < 44) return 0;
        var byteRate = BitConverter.ToInt32(wav, 28);
        if (byteRate <= 0) return 0;
        var dataIndex = IndexOfDataChunk(wav);
        if (dataIndex < 0) return 0;
        var dataSize = BitConverter.ToInt32(wav, dataIndex + 4);
        return dataSize / (double)byteRate * 1000.0;
    }

    static int IndexOfDataChunk(byte[] wav)
    {
        for (var i = 12; i < wav.Length - 8; i++)
        {
            if (wav[i] == 'd' && wav[i + 1] == 'a' && wav[i + 2] == 'a' && wav[i + 3] == 't')
                return i;
        }
        return -1;
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
