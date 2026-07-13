namespace BookPromoterAI;

static class ProcessTools
{
    /// <summary>Find a binary by absolute path, common Linux bin dirs, or PATH.</summary>
    public static string? ResolveBinary(params string[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (name.Contains('/') || name.Contains('\\'))
            {
                if (File.Exists(name))
                    return name;
                continue;
            }

            foreach (var dir in CandidateDirs())
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full))
                    return full;

                if (OperatingSystem.IsWindows())
                {
                    var exe = full + ".exe";
                    if (File.Exists(exe))
                        return exe;
                }
            }
        }

        return null;
    }

    static IEnumerable<string> CandidateDirs()
    {
        yield return "/usr/bin";
        yield return "/usr/local/bin";
        yield return "/bin";
        yield return "/usr/lib/ffmpeg";

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(dir))
                yield return dir;
        }
    }

    public static string QuoteArg(string value) =>
        $"\"{value.Replace("\"", "\\\"")}\"";
}
