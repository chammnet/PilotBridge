using System.Reflection;
using System.Text.Json;
using PilotBridge.Core;

namespace PilotBridge.Core.Tests;

/// <summary>
/// Keeps settings-help.jsonc in step with the code: a new setting without
/// help text, or a default changed in one place but not the other, fails here.
/// </summary>
public class SettingsHelpTests
{
    private static readonly SettingsHelp Help = SettingsHelp.Load();

    // Section in the help file -> the settings class it documents. Profile's
    // own collections (stick, buttons, knobs) are sections, not settings.
    public static TheoryData<string, Type> Sections => new()
    {
        { "profile", typeof(Profile) },
        { "stick", typeof(StickSettings) },
        { "axisTarget", typeof(AxisTarget) },
        { "knob", typeof(KnobSettings) },
    };

    private static IEnumerable<PropertyInfo> Settings(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Where(p => p.PropertyType.IsPrimitive || p.PropertyType.IsEnum || p.PropertyType == typeof(string));

    [Theory]
    [MemberData(nameof(Sections))]
    public void EverySetting_HasHelpText(string section, Type type)
    {
        var missing = Settings(type).Where(p => string.IsNullOrWhiteSpace(Help.Get(section, p.Name))).Select(p => p.Name);
        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void ListedDefaults_MatchTheCode(string section, Type type)
    {
        var fresh = Activator.CreateInstance(type)!;
        foreach (var p in Settings(type))
        {
            var listed = Help.Default(section, p.Name);
            Assert.True(listed.HasValue, $"{section}.{p.Name} has no default listed");
            var value = listed.Value.Deserialize(p.PropertyType, Profile.Options);
            Assert.True(Equals(p.GetValue(fresh), value), $"{section}.{p.Name}: code default {p.GetValue(fresh)}, help file says {value}");
        }
    }

    [Fact]
    public void Get_IsCaseInsensitive_AndNullForUnknown()
    {
        Assert.NotNull(Help.Get("knob", "FullScaleTicksPerSecond"));
        Assert.NotNull(Help.Get("knob", "fullScaleTicksPerSecond"));
        Assert.Null(Help.Get("knob", "noSuchSetting"));
        Assert.Null(Help.Get("noSuchSection", "curve"));
    }
}
