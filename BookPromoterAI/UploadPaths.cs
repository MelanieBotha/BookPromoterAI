namespace BookPromoterAI;

class UploadPaths
{
    public string Path { get; init; } = "";
    public bool UsesDataVolume { get; init; }

    public static UploadPaths Resolve(string dbPath, string contentRoot)
    {
        var fromEnv = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return new UploadPaths
            {
                Path = fromEnv,
                UsesDataVolume = fromEnv.Replace('\\', '/').Contains("/data/", StringComparison.OrdinalIgnoreCase)
            };
        }

        var usesData = dbPath.Replace('\\', '/').Contains("/data/", StringComparison.OrdinalIgnoreCase);
        return new UploadPaths
        {
            Path = usesData ? "/data/uploads" : System.IO.Path.Combine(contentRoot, "wwwroot", "uploads"),
            UsesDataVolume = usesData
        };
    }

    public static void EnsureReady(string uploadsDir, string contentRoot)
    {
        System.IO.Directory.CreateDirectory(uploadsDir);
        if (!uploadsDir.Replace('\\', '/').Contains("/data/", StringComparison.OrdinalIgnoreCase))
            return;

        var legacy = System.IO.Path.Combine(contentRoot, "wwwroot", "uploads");
        if (!System.IO.Directory.Exists(legacy)) return;

        foreach (var file in System.IO.Directory.GetFiles(legacy))
        {
            var dest = System.IO.Path.Combine(uploadsDir, System.IO.Path.GetFileName(file));
            if (!File.Exists(dest))
                File.Copy(file, dest);
        }
    }
}
