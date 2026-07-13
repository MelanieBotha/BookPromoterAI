using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookPromoterAI;

record SpeechWordTiming(string Word, double StartMs, double EndMs);

record SpeechAudioResult(
    byte[]? Data,
    string Extension,
    double DurationMs,
    string? Error,
    IReadOnlyList<SpeechWordTiming>? WordTimings = null,
    string Provider = "local")
{
    public bool Ok => Data is { Length: > 0 };
}

/// <summary>
/// Text-to-speech for narrated videos. Prefers ElevenLabs when configured (natural voice),
/// otherwise falls back to on-device espeak-ng / pico2wave / Windows SAPI.
/// </summary>
class LocalSpeechService
{
    /// <summary>ElevenLabs "Rachel" — clear, natural female voice good for book promos.</summary>
    public const string DefaultElevenLabsVoiceId = "21m00Tcm4TlvDq8ikWAM";

    static readonly Regex SafeText = new(@"[^\p{L}\p{N}\p{P}\p{Z}]", RegexOptions.Compiled);
    readonly AppSettings _settings;
    readonly IHttpClientFactory _httpFactory;
    string? _espeakPath;
    string? _picoPath;

    public LocalSpeechService(AppSettings settings, IHttpClientFactory httpFactory)
    {
        _settings = settings;
        _httpFactory = httpFactory;
    }

    public bool IsNaturalVoiceConfigured => _settings.IsElevenLabsConfigured;

    public bool IsAvailable => IsNaturalVoiceConfigured || EspeakPath() is not null || PicoPath() is not null
        || OperatingSystem.IsWindows();

    public string DiagnosticStatus()
    {
        if (IsNaturalVoiceConfigured)
            return $"ElevenLabs natural voice ({_settings.ElevenLabsVoiceId})";
        var espeak = EspeakPath();
        if (espeak is not null) return $"espeak ({espeak}) — robotic; set ElevenLabs__ApiKey for natural voice";
        var pico = PicoPath();
        if (pico is not null) return $"pico2wave ({pico}) — robotic; set ElevenLabs__ApiKey for natural voice";
        if (OperatingSystem.IsWindows()) return "Windows SAPI — set ElevenLabs__ApiKey for natural voice";
        return "none — set ElevenLabs__ApiKey or install espeak-ng";
    }

    public async Task<SpeechAudioResult> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var cleaned = Sanitize(text);
        if (string.IsNullOrWhiteSpace(cleaned))
            return new SpeechAudioResult(null, ".wav", 0, "Enter some text to read aloud.");

        if (IsNaturalVoiceConfigured)
        {
            var natural = await TryElevenLabsAsync(cleaned, cancellationToken);
            if (natural.Ok) return natural;
            // Fall through to local TTS if ElevenLabs fails so weekly videos still render.
        }

        if (OperatingSystem.IsWindows())
        {
            var windows = await TryWindowsSapiAsync(cleaned, cancellationToken);
            if (windows.Ok) return windows;
        }

        var espeak = await SynthesizeEspeakAsync(cleaned, cancellationToken);
        if (espeak.Ok) return espeak;

        var pico = await SynthesizePicoAsync(cleaned, cancellationToken);
        if (pico.Ok) return pico;

        var hint = IsNaturalVoiceConfigured
            ? "ElevenLabs failed and local TTS is unavailable."
            : "Read-aloud voice is robotic espeak, or unavailable. Add ElevenLabs__ApiKey in Railway for a natural voice.";
        return new SpeechAudioResult(null, ".wav", 0, espeak.Error ?? pico.Error ?? hint);
    }

    async Task<SpeechAudioResult> TryElevenLabsAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var voiceId = string.IsNullOrWhiteSpace(_settings.ElevenLabsVoiceId)
                ? DefaultElevenLabsVoiceId
                : _settings.ElevenLabsVoiceId.Trim();
            var url = $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}/with-timestamps";
            var payload = JsonSerializer.Serialize(new
            {
                text,
                model_id = "eleven_multilingual_v2",
                voice_settings = new { stability = 0.35, similarity_boost = 0.8, style = 0.35, use_speaker_boost = true }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("xi-api-key", _settings.ElevenLabsApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var http = _httpFactory.CreateClient(nameof(LocalSpeechService));
            http.Timeout = TimeSpan.FromMinutes(2);
            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var tip = body.Length > 180 ? body[..180] + "…" : body;
                return new SpeechAudioResult(null, ".mp3", 0, $"ElevenLabs error {(int)response.StatusCode}: {tip}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("audio_base64", out var audioProp))
                return new SpeechAudioResult(null, ".mp3", 0, "ElevenLabs response missing audio.");

            var audioBytes = Convert.FromBase64String(audioProp.GetString() ?? "");
            if (audioBytes.Length == 0)
                return new SpeechAudioResult(null, ".mp3", 0, "ElevenLabs returned empty audio.");

            IReadOnlyList<SpeechWordTiming>? timings = null;
            double durationMs = 0;
            if (root.TryGetProperty("alignment", out var alignment))
            {
                timings = GroupCharactersIntoWords(alignment);
                if (timings.Count > 0)
                    durationMs = timings[^1].EndMs;
            }

            if (durationMs <= 0)
                durationMs = EstimateMp3DurationMs(audioBytes);

            return new SpeechAudioResult(audioBytes, ".mp3", durationMs, null, timings, "elevenlabs");
        }
        catch (Exception ex)
        {
            return new SpeechAudioResult(null, ".mp3", 0, $"ElevenLabs: {ex.Message}");
        }
    }

    static IReadOnlyList<SpeechWordTiming> GroupCharactersIntoWords(JsonElement alignment)
    {
        if (!alignment.TryGetProperty("characters", out var charsEl) ||
            !alignment.TryGetProperty("character_start_times_seconds", out var startsEl) ||
            !alignment.TryGetProperty("character_end_times_seconds", out var endsEl))
            return [];

        var chars = charsEl.EnumerateArray().Select(c => c.GetString() ?? "").ToList();
        var starts = startsEl.EnumerateArray().Select(c => c.GetDouble()).ToList();
        var ends = endsEl.EnumerateArray().Select(c => c.GetDouble()).ToList();
        if (chars.Count == 0 || chars.Count != starts.Count || chars.Count != ends.Count)
            return [];

        var words = new List<SpeechWordTiming>();
        var current = new StringBuilder();
        double wordStart = 0;
        var inWord = false;

        for (var i = 0; i < chars.Count; i++)
        {
            var ch = chars[i];
            if (string.IsNullOrWhiteSpace(ch))
            {
                if (inWord)
                {
                    words.Add(new SpeechWordTiming(current.ToString(), wordStart * 1000, ends[i - 1] * 1000));
                    current.Clear();
                    inWord = false;
                }
                continue;
            }

            if (!inWord)
            {
                wordStart = starts[i];
                inWord = true;
            }
            current.Append(ch);
        }

        if (inWord && current.Length > 0)
            words.Add(new SpeechWordTiming(current.ToString(), wordStart * 1000, ends[^1] * 1000));

        return words;
    }

    static double EstimateMp3DurationMs(byte[] mp3)
    {
        // Rough fallback when alignment is missing (~16 KB/s for 128kbps).
        if (mp3.Length < 1024) return 5000;
        return Math.Max(3000, mp3.Length / 16.0);
    }

    static string Sanitize(string text)
    {
        var limited = ReadAloudScript.LimitWords(text.Trim());
        var stripped = SafeText.Replace(limited, " ");
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    static async Task<SpeechAudioResult> TryWindowsSapiAsync(string text, CancellationToken cancellationToken)
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
                return new SpeechAudioResult(null, ".wav", 0, null);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(tempWav))
                return new SpeechAudioResult(null, ".wav", 0, null);
            var bytes = await File.ReadAllBytesAsync(tempWav, cancellationToken);
            return new SpeechAudioResult(bytes, ".wav", WavDurationMs(bytes), null, null, "sapi");
        }
        catch
        {
            return new SpeechAudioResult(null, ".wav", 0, null);
        }
        finally
        {
            TryDelete(tempWav);
            TryDelete(tempTxt);
            TryDelete(tempPs1);
        }
    }

    async Task<SpeechAudioResult> SynthesizeEspeakAsync(string text, CancellationToken cancellationToken)
    {
        var espeak = EspeakPath();
        if (espeak is null)
            return new SpeechAudioResult(null, ".wav", 0, null);

        var tempWav = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.wav");
        var tempTxt = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempTxt, text, Encoding.UTF8, cancellationToken);
            var psi = new ProcessStartInfo
            {
                FileName = espeak,
                Arguments = $"-v en-us -s 150 -p 45 -w {ProcessTools.QuoteArg(tempWav)} -f {ProcessTools.QuoteArg(tempTxt)}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return new SpeechAudioResult(null, ".wav", 0, "Could not start espeak.");
            var err = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(tempWav))
                return new SpeechAudioResult(null, ".wav", 0, string.IsNullOrWhiteSpace(err) ? "espeak synthesis failed." : err.Trim());

            var bytes = await File.ReadAllBytesAsync(tempWav, cancellationToken);
            return new SpeechAudioResult(bytes, ".wav", WavDurationMs(bytes), null, null, "espeak");
        }
        catch (Exception ex)
        {
            return new SpeechAudioResult(null, ".wav", 0, ex.Message);
        }
        finally
        {
            TryDelete(tempWav);
            TryDelete(tempTxt);
        }
    }

    async Task<SpeechAudioResult> SynthesizePicoAsync(string text, CancellationToken cancellationToken)
    {
        var pico = PicoPath();
        if (pico is null)
            return new SpeechAudioResult(null, ".wav", 0, null);

        var tempWav = Path.Combine(Path.GetTempPath(), $"bpa-speech-{Guid.NewGuid():N}.wav");
        try
        {
            var spoken = text.Length > 900 ? text[..900] : text;
            var psi = new ProcessStartInfo
            {
                FileName = pico,
                Arguments = $"-w {ProcessTools.QuoteArg(tempWav)} -l en-US {ProcessTools.QuoteArg(spoken)}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return new SpeechAudioResult(null, ".wav", 0, "Could not start pico2wave.");
            var err = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(tempWav))
                return new SpeechAudioResult(null, ".wav", 0, string.IsNullOrWhiteSpace(err) ? "pico2wave synthesis failed." : err.Trim());

            var bytes = await File.ReadAllBytesAsync(tempWav, cancellationToken);
            return new SpeechAudioResult(bytes, ".wav", WavDurationMs(bytes), null, null, "pico");
        }
        catch (Exception ex)
        {
            return new SpeechAudioResult(null, ".wav", 0, ex.Message);
        }
        finally
        {
            TryDelete(tempWav);
        }
    }

    string? EspeakPath() => _espeakPath ??= ProcessTools.ResolveBinary(
        "/usr/bin/espeak-ng", "/usr/bin/espeak", "espeak-ng", "espeak");

    string? PicoPath() => _picoPath ??= ProcessTools.ResolveBinary(
        "/usr/bin/pico2wave", "pico2wave");

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
