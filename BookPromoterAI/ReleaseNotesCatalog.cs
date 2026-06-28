using System.Text.Json;

namespace BookPromoterAI;

class ReleaseNoteDraft
{
    public string Title { get; init; } = "";
    public List<string> Updated { get; init; } = [];
    public List<string> New { get; init; } = [];
    public List<string> Added { get; init; } = [];

    public string UpdatedText => string.Join("\n", Updated);
    public string NewText => string.Join("\n", New);
    public string AddedText => string.Join("\n", Added);

    public bool HasContent => Updated.Count > 0 || New.Count > 0 || Added.Count > 0;

    public static ReleaseNoteDraft ForVersion(string version)
    {
        return new ReleaseNoteDraft
        {
            Title = $"BookPromoter AI v{version} — What's new"
        };
    }
}

class ReleaseNotesCatalog
{
    private readonly Dictionary<string, ReleaseNoteDraft> _byVersion;

    public ReleaseNotesCatalog(IWebHostEnvironment env)
    {
        _byVersion = Load(Path.Combine(env.ContentRootPath, "ReleaseNotes.json"));
    }

    public ReleaseNoteDraft GetDraft(string version)
    {
        if (_byVersion.TryGetValue(version, out var draft))
            return draft;
        return ReleaseNoteDraft.ForVersion(version);
    }

    static Dictionary<string, ReleaseNoteDraft> Load(string path)
    {
        if (!File.Exists(path))
            return new(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var result = new Dictionary<string, ReleaseNoteDraft>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    continue;
                result[property.Name] = new ReleaseNoteDraft
                {
                    Title = property.Value.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                    Updated = ReadStringArray(property.Value, "updated"),
                    New = ReadStringArray(property.Value, "new"),
                    Added = ReadStringArray(property.Value, "added")
                };
            }
            return result;
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    static List<string> ReadStringArray(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
            return [];
        return array.EnumerateArray()
            .Select(e => e.GetString()?.Trim() ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
