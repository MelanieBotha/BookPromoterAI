using System.Reflection;

namespace BookPromoterAI;

/// <summary>Single source for the app version. Bump &lt;Version&gt; in BookPromoterAI.csproj on every release.</summary>
static class AppVersion
{
    public static string Display
    {
        get
        {
            var info = typeof(AppVersion).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                return plus > 0 ? info[..plus] : info;
            }
            return typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        }
    }
}
