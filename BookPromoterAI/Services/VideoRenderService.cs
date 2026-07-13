using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace BookPromoterAI;

/// <summary>
/// Renders ~60s vertical book promo videos (FFmpeg Ken Burns + local TTS + burned captions).
/// Tuned for Railway: 720×1280 H.264, ultrafast preset, process timeout.
/// </summary>
class VideoRenderService
{
    // 720×1280 is TikTok-safe and much faster on small Railway CPUs than 1080×1920.
    public const int TargetWidth = 720;
    public const int TargetHeight = 1280;
    const int Fps = 24;
    static readonly TimeSpan FfmpegTimeout = TimeSpan.FromMinutes(8);

    readonly LocalSpeechService _speech;
    readonly IHttpClientFactory _httpFactory;
    string? _ffmpegPath;
    string? _ffprobePath;

    public VideoRenderService(LocalSpeechService speech, IHttpClientFactory httpFactory, AppSettings settings)
    {
        _speech = speech;
        _httpFactory = httpFactory;
        _ = settings;
    }

    public bool IsFfmpegAvailable => FfmpegPath() is not null;

    public string FfmpegDiagnosticStatus()
    {
        var path = FfmpegPath();
        return path is null ? "missing — install ffmpeg in Docker/Nixpacks" : path;
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

        if (FfmpegPath() is null)
            return (false, null,
                "FFmpeg is not installed on this server. Redeploy with the root Dockerfile (includes ffmpeg) or Nixpacks aptPkgs.");

        var script = TrimToTargetDuration(narrationText, TikTokVideoLimits.MaxExcerptWords);
        var workDir = Path.Combine(Path.GetTempPath(), $"bpa-video-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try
        {
            var coverPath = await ResolveCoverPathAsync(book, uploadsDir, appBaseUrl, workDir, cancellationToken);
            if (coverPath is null)
                return (false, null, "Add a book cover in Books before generating videos.");

            var speech = await _speech.SynthesizeAsync(script, cancellationToken);
            if (!speech.Ok || speech.Data is null)
            {
                // Do not render a silent "caption promo" when natural voice is expected —
                // that looks like "audio disappeared." Surface the real TTS error instead.
                if (_speech.IsNaturalVoiceConfigured)
                    return (false, null, speech.Error ?? "ElevenLabs voice failed. Check ElevenLabs__ApiKey and credits, then Retry.");

                return await RenderCaptionPromoVideoAsync(
                    coverPath, script, uploadsDir, speech.Error, cancellationToken);
            }

            var audioPath = Path.Combine(workDir, "speech" + speech.Extension);
            await File.WriteAllBytesAsync(audioPath, speech.Data, cancellationToken);
            Console.WriteLine($"[Video] Narration ready via {speech.Provider} ({speech.Data.Length} bytes, {speech.Extension}).");

            var measuredSec = GetMediaDurationSeconds(audioPath);
            if (measuredSec <= 0)
                measuredSec = speech.DurationMs / 1000.0;
            if (measuredSec <= 0.5)
                return (false, null, "Narration audio was empty or too short. Retry, or check ElevenLabs credits.");

            var speechMs = TikTokVideoLimits.ClampSpeechMs(measuredSec * 1000.0);
            var totalSec = Math.Min(
                TikTokVideoLimits.MaxDurationMs / 1000.0,
                Math.Max(3.0, speechMs / 1000.0 + 1.5));

            var plan = speech.WordTimings is { Count: > 0 }
                ? ReadAloudScript.BuildFromWordTimings(speech.WordTimings)
                : ReadAloudScript.BuildWordChunks(script, speechMs);
            var srtPath = Path.Combine(workDir, "subs.srt");
            await File.WriteAllTextAsync(srtPath, BuildSrt(plan), Encoding.UTF8, cancellationToken);

            var outName = $"bookpromo-{Guid.NewGuid():N}.mp4";
            var outPath = Path.Combine(uploadsDir, outName);
            Directory.CreateDirectory(uploadsDir);

            var (ok, ffmpegError) = await RunNarratedFfmpegAsync(
                coverPath, audioPath, srtPath, outPath, totalSec, cancellationToken);
            if (!ok || !File.Exists(outPath))
                return (false, null, ffmpegError ?? "Video rendering failed. Check FFmpeg on the server.");

            return (true, $"/uploads/{outName}", null);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* ignore */ }
        }
    }

    async Task<(bool Ok, string? VideoUrl, string? Error)> RenderCaptionPromoVideoAsync(
        string coverPath,
        string narrationText,
        string uploadsDir,
        string? speechError,
        CancellationToken cancellationToken)
    {
        var workDir = Path.GetDirectoryName(coverPath) ?? Path.GetTempPath();
        var speechMs = TikTokVideoLimits.MaxSpeechMs;
        var plan = ReadAloudScript.BuildWordChunks(narrationText, speechMs);
        var totalSec = TikTokVideoLimits.MaxDurationMs / 1000.0;

        var srtPath = Path.Combine(workDir, "subs-promo.srt");
        await File.WriteAllTextAsync(srtPath, BuildSrt(plan), Encoding.UTF8, cancellationToken);

        var outName = $"bookpromo-{Guid.NewGuid():N}.mp4";
        var outPath = Path.Combine(uploadsDir, outName);
        Directory.CreateDirectory(uploadsDir);

        var (ok, ffmpegError) = await RunCaptionOnlyFfmpegAsync(coverPath, srtPath, outPath, totalSec, cancellationToken);
        if (!ok || !File.Exists(outPath))
        {
            var detail = ffmpegError ?? speechError ?? "Could not render caption video.";
            return (false, null, detail);
        }

        return (true, $"/uploads/{outName}", null);
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

    async Task<(bool Ok, string? Error)> RunNarratedFfmpegAsync(
        string coverPath, string audioPath, string srtPath, string outPath, double totalSec,
        CancellationToken cancellationToken)
    {
        var ffmpeg = FfmpegPath();
        if (ffmpeg is null) return (false, "FFmpeg not found.");

        var frames = Math.Max(1, (int)Math.Ceiling(totalSec * Fps));
        var vf = BuildKenBurnsFilter(frames, srtPath);
        var duration = totalSec.ToString("0.###", CultureInfo.InvariantCulture);
        // Explicit maps: cover has no audio — without -map 1:a:0 FFmpeg can emit a silent video.
        var args =
            $"-y -loop 1 -i {ProcessTools.QuoteArg(coverPath)} -i {ProcessTools.QuoteArg(audioPath)} " +
            $"-vf \"{vf}\" -map 0:v:0 -map 1:a:0 " +
            $"-c:v libx264 -preset ultrafast -crf 28 -pix_fmt yuv420p " +
            $"-c:a aac -b:a 128k -ac 2 -ar 44100 -shortest -t {duration} " +
            $"-movflags +faststart {ProcessTools.QuoteArg(outPath)}";

        return await RunFfmpegAsync(ffmpeg, args, cancellationToken);
    }

    async Task<(bool Ok, string? Error)> RunCaptionOnlyFfmpegAsync(
        string coverPath, string srtPath, string outPath, double totalSec,
        CancellationToken cancellationToken)
    {
        var ffmpeg = FfmpegPath();
        if (ffmpeg is null) return (false, "FFmpeg not found.");

        var frames = Math.Max(1, (int)Math.Ceiling(totalSec * Fps));
        var vf = BuildKenBurnsFilter(frames, srtPath);
        var duration = totalSec.ToString("0.###", CultureInfo.InvariantCulture);
        var args =
            $"-y -loop 1 -i {ProcessTools.QuoteArg(coverPath)} " +
            $"-f lavfi -i anullsrc=channel_layout=mono:sample_rate=44100 " +
            $"-vf \"{vf}\" -c:v libx264 -preset ultrafast -crf 28 -pix_fmt yuv420p " +
            $"-c:a aac -b:a 64k -t {duration} -movflags +faststart {ProcessTools.QuoteArg(outPath)}";

        return await RunFfmpegAsync(ffmpeg, args, cancellationToken);
    }

    /// <summary>Crop to vertical, light Ken Burns zoom, burn SRT — no 2× upscale (too slow on Railway).</summary>
    static string BuildKenBurnsFilter(int frames, string srtPath)
    {
        var srtFilter = EscapeForFfmpegFilter(srtPath);
        var style =
            "FontName=DejaVu Sans,FontSize=22,Bold=1,PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000," +
            "BorderStyle=3,Outline=2,Shadow=0,Alignment=2,MarginV=100";

        return
            $"scale={TargetWidth}:{TargetHeight}:force_original_aspect_ratio=increase," +
            $"crop={TargetWidth}:{TargetHeight}," +
            $"zoompan=z='min(zoom+0.0010,1.22)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={frames}:s={TargetWidth}x{TargetHeight}:fps={Fps}," +
            $"format=yuv420p," +
            $"subtitles='{srtFilter}':force_style='{style}'";
    }

    static async Task<(bool Ok, string? Error)> RunFfmpegAsync(
        string ffmpeg, string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi);
        if (process is null) return (false, "Could not start FFmpeg.");

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(FfmpegTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            if (cancellationToken.IsCancellationRequested)
                return (false, "Video rendering was cancelled.");
            return (false, $"FFmpeg timed out after {(int)FfmpegTimeout.TotalMinutes} minutes. Click Retry.");
        }

        var stderr = await stderrTask;

        if (process.ExitCode == 0) return (true, null);

        var tip = string.IsNullOrWhiteSpace(stderr)
            ? "FFmpeg failed with no error output."
            : TrimError(stderr);
        return (false, tip);
    }

    double GetMediaDurationSeconds(string filePath)
    {
        var ffprobe = FfprobePath();
        if (ffprobe is null) return 0;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {ProcessTools.QuoteArg(filePath)}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return 0;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(8000);
            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static string TrimToTargetDuration(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length <= maxWords) return string.Join(' ', words);

        var trimmed = string.Join(' ', words.Take(maxWords));
        var lastPeriod = trimmed.LastIndexOf('.');
        if (lastPeriod > trimmed.Length / 2)
            return trimmed[..(lastPeriod + 1)].Trim();
        return trimmed.TrimEnd('.', ',', ';', ':') + "...";
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
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }

    static string EscapeForFfmpegFilter(string path) =>
        path.Replace("\\", "/").Replace(":", "\\:").Replace("'", "\\'");

    static string TrimError(string stderr)
    {
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var last = lines.Reverse().FirstOrDefault(l =>
            l.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Error", StringComparison.Ordinal) ||
            l.Contains("Invalid", StringComparison.OrdinalIgnoreCase));
        var tip = last ?? lines.LastOrDefault() ?? stderr;
        return tip.Length > 240 ? tip[..240] + "…" : tip;
    }

    string? FfmpegPath() => _ffmpegPath ??= ProcessTools.ResolveBinary(
        "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg", "ffmpeg");

    string? FfprobePath() => _ffprobePath ??= ProcessTools.ResolveBinary(
        "/usr/bin/ffprobe", "/usr/local/bin/ffprobe", "ffprobe");
}
