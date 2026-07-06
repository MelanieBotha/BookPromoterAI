namespace BookPromoterAI;

static class ProcessTools
{
    /// <summary>Find a binary by absolute path or common Linux bin dirs (no subprocess).</summary>
    public static string? ResolveBinary(params string[] names)
    {
        foreach (var name in names)
        {
            if (name.Contains('/'))
            {
                if (File.Exists(name))
                    return name;
                continue;
            }

            foreach (var dir in new[] { "/usr/bin", "/usr/local/bin", "/bin" })
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full))
                    return full;
            }
        }

        return null;
    }

    public static string QuoteArg(string value) =>
        $"\"{value.Replace("\"", "\\\"")}\"";
}
