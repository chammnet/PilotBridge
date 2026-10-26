using System.Text.Json;

namespace PilotBridge.Core;

/// <summary>
/// Help text for profile settings, read from settings-help.jsonc: each
/// setting "name" has its help in a sibling key "_name". Sections:
/// profile, stick, axisTarget, knob.
/// </summary>
public sealed class SettingsHelp
{
    public const string FileName = "settings-help.jsonc";

    private readonly JsonDocument _doc;

    private SettingsHelp(JsonDocument doc) => _doc = doc;

    public static SettingsHelp Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, FileName);
        var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        return new SettingsHelp(doc);
    }

    /// <summary>Help text for a setting, or null if there is none.</summary>
    public string? Get(string section, string setting) =>
        Find(section, "_" + setting) is { ValueKind: JsonValueKind.String } help ? help.GetString() : null;

    /// <summary>The default value listed for a setting, if any.</summary>
    internal JsonElement? Default(string section, string setting) => Find(section, setting);

    private JsonElement? Find(string section, string key)
    {
        if (!_doc.RootElement.TryGetProperty(section, out var s))
            return null;
        // Case-insensitive, like profile loading.
        foreach (var p in s.EnumerateObject())
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }
}
