using System.Diagnostics;

namespace BookPromoterAI;

static class ProcessTools
{
    public static string? FindExecutable(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Contains('/') && !File.Exists(candidate))
                continue;

            foreach (var versionArg in new[] { "--version", "-version" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = versionArg,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    if (process is null) continue;
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                        return candidate;
                }
                catch { /* try next */ }
            }
        }

        return null;
    }

    public static string QuoteArg(string value) =>
        $"\"{value.Replace("\"", "\\\"")}\"";
}
