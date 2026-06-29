namespace BookPromoterAI;

static class CoverImageInfo
{
    public static (int Width, int Height, string ContentType)? TryGetLocal(string uploadsDir, string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl) ||
            !coverUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = Path.Combine(uploadsDir, Path.GetFileName(coverUrl));
        if (!File.Exists(path)) return null;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var dims = TryReadDimensions(path);
        return dims is null ? (0, 0, contentType) : (dims.Value.Width, dims.Value.Height, contentType);
    }

    static (int Width, int Height)? TryReadDimensions(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[32];
            var read = stream.Read(header);
            if (read < 24) return null;

            // PNG: IHDR width/height at bytes 16-23 (big-endian)
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                return (ReadInt32BigEndian(header[16..20]), ReadInt32BigEndian(header[20..24]));
            }

            // JPEG: scan for SOF0/SOF2 marker
            if (header[0] == 0xFF && header[1] == 0xD8)
            {
                stream.Position = 2;
                var buffer = new byte[4096];
                while (stream.Read(buffer, 0, buffer.Length) is int count and > 1)
                {
                    for (var i = 0; i < count - 1; i++)
                    {
                        if (buffer[i] != 0xFF) continue;
                        var marker = buffer[i + 1];
                        if (marker is 0xC0 or 0xC2 && i + 8 < count)
                        {
                            var height = (buffer[i + 5] << 8) | buffer[i + 6];
                            var width = (buffer[i + 7] << 8) | buffer[i + 8];
                            if (width > 0 && height > 0) return (width, height);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore unreadable files — OG tags still work without dimensions.
        }

        return null;
    }

    static int ReadInt32BigEndian(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
}
