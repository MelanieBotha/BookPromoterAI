namespace BookPromoterAI;

class DatabasePaths
{
    public string Path { get; init; } = "";
    public bool FileExists { get; init; }

    public bool UsesDataVolume =>
        Path.Replace('\\', '/').Contains("/data/", StringComparison.OrdinalIgnoreCase);

    public string StatusSummary => UsesDataVolume
        ? (FileExists
            ? "Persistent storage (/data volume) - customer data survives redeploys."
            : "Persistent volume mounted at /data - database will be created on first run.")
        : (FileExists
            ? "Ephemeral storage - data may reset when Railway redeploys. Add a /data volume."
            : "Ephemeral storage - add a Railway volume mounted at /data.");

    public static DatabasePaths Resolve(string dbPath)
    {
        var exists = File.Exists(dbPath);
        return new DatabasePaths { Path = dbPath, FileExists = exists };
    }
}
