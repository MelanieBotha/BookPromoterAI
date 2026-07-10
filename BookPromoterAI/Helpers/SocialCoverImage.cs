using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BookPromoterAI;

/// <summary>Letterboxes portrait book covers for X/Facebook link-preview cards (2:1).</summary>
static class SocialCoverImage
{
    public const int CardWidth = 1200;
    public const int CardHeight = 628;

    public static byte[]? TryBuildCard(string coverPath)
    {
        if (!File.Exists(coverPath)) return null;

        try
        {
            using var cover = Image.Load<Rgba32>(coverPath);
            using var canvas = new Image<Rgba32>(CardWidth, CardHeight, new Rgba32(26, 26, 26));

            var scale = Math.Min((float)CardWidth / cover.Width, (float)CardHeight / cover.Height);
            var targetW = Math.Max(1, (int)Math.Round(cover.Width * scale));
            var targetH = Math.Max(1, (int)Math.Round(cover.Height * scale));
            var offsetX = (CardWidth - targetW) / 2;
            var offsetY = (CardHeight - targetH) / 2;

            cover.Mutate(ctx => ctx.Resize(targetW, targetH));
            canvas.Mutate(ctx => ctx.DrawImage(cover, new Point(offsetX, offsetY), 1f));

            using var stream = new MemoryStream();
            canvas.Save(stream, new JpegEncoder { Quality = 90 });
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
