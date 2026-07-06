namespace BookPromoterAI;

/// <summary>TikTok standard vertical video length (60 seconds).</summary>
static class TikTokVideoLimits
{
    public const int MaxDurationMs = 60_000;
    public const int NarratedCtaMs = 4_000;
    public const int MaxSpeechMs = MaxDurationMs - NarratedCtaMs;
    public const int SpeechWordsPerMinute = 165;

    /// <summary>Max excerpt words that fit in <see cref="MaxSpeechMs"/> at typical read speed.</summary>
    public const int MaxExcerptWords = 155;

    public static double ClampSpeechMs(double durationMs) =>
        Math.Min(durationMs, MaxSpeechMs);
}
