using System.Globalization;
using PilotBridge.Core;

namespace PilotBridge.App;

/// <summary>Scores a --calibrate recording against a profile's knob settings.</summary>
internal static class Replay
{
    public static int Run(string csvPath, PilotKnob knob, string profilePath)
    {
        var profile = Profile.Load(profilePath);
        var settings = profile.Knobs.GetValueOrDefault(knob) ?? new KnobSettings();
        if (settings.Mode != KnobMode.Rate)
        {
            Console.WriteLine($"{knob} is in {settings.Mode} mode in {profile.Name}; replay scores Rate mode only.");
            return 1;
        }

        var rows = File.ReadAllLines(csvPath).Skip(1)
            .Select(line => line.Split(','))
            .Select(p => (Level: p[0], T: double.Parse(p[1], CultureInfo.InvariantCulture), Dir: int.Parse(p[2])))
            .ToList();

        Console.WriteLine($"{Path.GetFileName(csvPath)} through {profile.Name} / {knob}:");
        Console.WriteLine($"  average {settings.AverageIntervals} gaps, grace {settings.GraceFactor}x, " +
                          $"smoothing {settings.SmoothingSeconds}s, ramp up/down {settings.RampUpPerSecond}/{settings.RampDownPerSecond}, " +
                          $"full scale {settings.FullScaleTicksPerSecond} ticks/s");
        Console.WriteLine();
        Console.WriteLine("level          ticks/s  output  bounce  to speed  to stop");
        foreach (var level in rows.Select(r => r.Level).Distinct())
        {
            var ticks = rows.Where(r => r.Level == level).Select(r => (r.T, r.Dir)).ToList();
            var s = KnobReplay.Run(ticks, settings);
            Console.WriteLine($"{level,-13}  {s.TicksPerSecond,7:0.0}  {s.MeanOutput,6:0.00}  {s.Bounce,6:0%}  " +
                              $"{s.SecondsToSpeed,7:0.00}s  {s.SecondsToStop,6:0.00}s");
        }
        Console.WriteLine();
        Console.WriteLine("output = average axis value while turning (1.00 = full);");
        Console.WriteLine("bounce = how much it wobbles around that (lower is smoother).");
        return 0;
    }
}
