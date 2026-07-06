namespace BookPromoterAI;

static class FileHelpers
{
    public static async Task<string?> SaveVideoUpload(IFormFile? file, string uploadsDir)
    {
        if (file is null || file.Length == 0) return null;
        const long maxBytes = 1024L * 1024 * 1024; // 1 GB TikTok limit
        if (file.Length > maxBytes) return null;
        var allowedExtensions = new[] { ".mp4", ".mov", ".webm", ".avi" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension)) return null;
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);
        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream);
        return $"/uploads/{fileName}";
    }

    public static async Task<string?> SaveCoverUpload(IFormFile? file, string uploadsDir)
    {
        if (file is null || file.Length == 0) return null;
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension)) return null;
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);
        using var stream = File.Create(filePath);
        await file.CopyToAsync(stream);
        return $"/uploads/{fileName}";
    }

    public static List<BookLink> ParseLinks(IFormCollection form)
    {
        var stores = form["linkStore"].ToList();
        var urls = form["linkUrl"].ToList();
        var links = new List<BookLink>();
        for (var i = 0; i < stores.Count && i < urls.Count; i++)
        {
            var storeName = (stores[i] ?? "").Trim();
            var url = (urls[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(storeName)) continue;
            if (storeName == "__custom__")
            {
                var customNames = form["linkStoreCustom"].ToList();
                storeName = i < customNames.Count ? (customNames[i] ?? "").Trim() : "Other";
                if (string.IsNullOrWhiteSpace(storeName)) storeName = "Other";
            }
            if (string.IsNullOrWhiteSpace(url)) continue;
            links.Add(new BookLink { StoreName = storeName, Url = url });
        }
        return links;
    }
}
