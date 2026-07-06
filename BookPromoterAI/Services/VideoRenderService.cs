using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace BookPromoterAI;

/// <summary>Renders 60-second vertical book promo videos on the server (FFmpeg + local TTS).</summary>
class VideoRenderService
{
    readonly LocalSpeechService _speech;
    readonly IHttpClientFactory _httpFactory;
    readonly AppSettings _settings;
    string? _ffmpegPath;

    public VideoRenderService(LocalSpeechService speech, IHttpClientFactory httpFactory, AppSettings settings)
    {
        _speech = speech;
        _httpFactory = httpFactory;
        _settings = settings;
    }

    public async Task<(bool Ok, string? VideoUrl, string? Error)> RenderNarratedVideoAsync(
        Book book,
        string narrationText,
        string uploadsDir,
        string appBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(narrationText))
            return (false, null, "No narration text for this video.");

        var workDir = Path.Combine(Path.GetTempPath(), $"bpa-video-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try
        {
            var coverPath = await ResolveCoverPathAsync(book, uploadsDir, appBaseUrl, workDir, cancellationToken);
            if (coverPath is null)
                return (false, null, "Add a book cover in Books before generating videos.");

            var (wav, durationMs, speechError) = await _speech.SynthesizeAsync(narrationText, cancellationToken);
            if (wav is null)
                return (false, null, speechError ?? "Speech synthesis failed.");

            var wavPath = Path.Combine(workDir, "speech.wav");
            await File.WriteAllBytesAsync(wavPath, wav, cancellationToken);

            var speechMs = TikTokVideoLimits.ClampSpeechMs(durationMs);
            var totalSec = Math.Min(TikTokVideoLimits.MaxDurationMs / 1000.0, speechMs / 1000.0 + 2.0);
            var frames = (int)Math.Ceiling(totalSec * 30);

            var plan = ReadAloudScript.Build(narrationText, speechMs);
            var srtPath = Path.Combine(workDir, "subs.srt");
            await File.WriteAllTextAsync(srtPath, BuildSrt(plan), Encoding.UTF8, cancellationToken);

            var outName = $"bookpromo-{Guid.NewGuid():N}.webm";
            var outPath = Path.Combine(uploadsDir, outName);
            Directory.CreateDirectory(uploadsDir);

            var ok = await RunFfmpegAsync(coverPath, wavPath, srtPath, outPath, frames, cancellationToken);
            if (!ok || !File.Exists(outPath))
                return (false, null, "Video rendering failed. FFmpeg may not be installed on the server.");

            return (true, $"/uploads/{outName}", null);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* ignore */ }
        }
    }

    async Task<string?> ResolveCoverPathAsync(
        Book book, string uploadsDir, string appBaseUrl, string workDir, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(book.CoverImageUrl) &&
            book.CoverImageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var local = Path.Combine(uploadsDir, Path.GetFileName(book.CoverImageUrl));
            if (File.Exists(local))
            {
                var dest = Path.Combine(workDir, "cover" + Path.GetExtension(local));
                File.Copy(local, dest, true);
                return dest;
            }
        }

        var http = _httpFactory.CreateClient(nameof(VideoRenderService));
        var attachment = await BookCoverLoader.TryLoadAsync(
            http, uploadsDir, appBaseUrl, book.Title, book.CoverImageUrl, book.TrackingCode, cancellationToken);
        if (attachment is null) return null;

        var ext = attachment.MimeType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        var path = Path.Combine(workDir, "cover" + ext);
        await File.WriteAllBytesAsync(path, attachment.Data, cancellationToken);
        return path;
    }

    static string BuildSrt(ReadAloudPlan plan)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < plan.Beats.Count; i++)
        {
            var beat = plan.Beats[i];
            sb.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
            sb.Append(FormatSrtTime(beat.StartMs));
            sb.Append(" --> ");
            sb.AppendLine(FormatSrtTime(beat.EndMs));
            sb.AppendLine(beat.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static string FormatSrtTime(double ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }

    async Task<bool> RunFfmpegAsync(
        string coverPath, string wavPath, string srtPath, string outPath, int frames, CancellationToken cancellationToken)
    {
        var ffmpeg = FfmpegPath();
        if (ffmpeg is null) return false;

        var srtFilter = srtPath.Replace("\\", "/").Replace(":", "\\:");
        var vf = $"scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280," +
                 $"zoompan=z='min(zoom+0.0010,1.22)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={frames}:s=720x1280:fps=30," +
                 $"subtitles='{srtFilter}':force_style='FontName=Arial,FontSize=22,PrimaryColour=&HFFFFFF,OutlineColour=&H000000,BorderStyle=3,Alignment=2'";

        var args = $"-y -loop 1 -i \"{coverPath}\" -i \"{wavPath}\" -vf \"{vf}\" -c:v libvpx-vp9 -b:v 2M -c:a libopus -pix_fmt yuv420p -shortest \"{outPath}\"";
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi);
        if (process is null) return false;
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0;
    }

    string? FfmpegPath() => _ffmpegPath ??= ProcessTools.ResolveBinary(
        "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg", "ffmpeg");
}
