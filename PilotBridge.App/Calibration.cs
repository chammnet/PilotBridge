using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using PilotBridge.Core;

namespace PilotBridge.App;

/// <summary>
/// Records how fast someone actually turns a knob at five self-judged speeds,
/// so Rate-mode settings can be chosen (and replayed offline) from real data.
/// </summary>
internal static class Calibration
{
    private static readonly string[] Levels = ["slow", "medium-slow", "medium", "medium-fast", "fast"];

    public static int Run(PilotKnob knob)
    {
        using var device = PilotDevice.Open();
        if (device is null)
        {
            Console.WriteLine("Pilot Pro not found or could not be opened. Plugged in over USB?");
            return 1;
        }

        var clock = Stopwatch.StartNew();
        var ticks = new ConcurrentQueue<(double T, int Dir)>();
        var reader = new Thread(() =>
        {
            try
            {
                while (true)
                    if (PilotPro.Parse(device.Read()) is KnobTick k && k.Knob == knob)
                        ticks.Enqueue((clock.Elapsed.TotalSeconds, k.Direction));
            }
            catch (Exception)
            {
                // Device closed at the end of the run.
            }
        }) { IsBackground = true };
        reader.Start();
        device.SendRawModeCommand();

        Console.WriteLine($"Calibrating {knob}. Turn it CLOCKWISE, as steadily as feels natural.");
        Console.WriteLine("For each speed: press Enter, turn for about 10 seconds, press Enter again.");

        var recorded = new List<(string Level, double T, int Dir)>();
        foreach (var level in Levels)
        {
            Console.WriteLine();
            Console.Write($"[{level}] Press Enter to start...");
            Console.ReadLine();
            ticks.Clear();
            Console.Write($"[{level}] Recording. Press Enter to stop...");
            Console.ReadLine();
            while (ticks.TryDequeue(out var tick))
                recorded.Add((level, tick.T, tick.Dir));
            Console.WriteLine($"[{level}] {recorded.Count(r => r.Level == level)} ticks.");
        }
        device.Dispose();

        var path = Path.Combine("logs", $"calibration-{knob}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        Directory.CreateDirectory("logs");
        File.WriteAllLines(path, recorded
            .Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.Level},{r.T:0.000000},{r.Dir}"))
            .Prepend("level,seconds,direction"));

        Console.WriteLine();
        Console.WriteLine("level          ticks  ticks/s  ticks/min  unevenness  middle 80% of speeds");
        foreach (var level in Levels)
        {
            var times = recorded.Where(r => r.Level == level).Select(r => r.T).ToList();
            Console.WriteLine($"{level,-13}  {Summarize(times)}");
        }
        Console.WriteLine();
        Console.WriteLine("Unevenness = how much the gap between ticks varies (0% = metronome steady).");
        Console.WriteLine($"Saved: {Path.GetFullPath(path)}");
        return 0;
    }

    private static string Summarize(List<double> times)
    {
        if (times.Count < 3)
            return $"{times.Count,5}  (too few ticks)";

        var intervals = times.Zip(times.Skip(1), (a, b) => b - a).ToList();
        var mean = intervals.Average();
        var sd = Math.Sqrt(intervals.Sum(i => (i - mean) * (i - mean)) / intervals.Count);
        var rate = (times.Count - 1) / (times[^1] - times[0]);

        // Per-interval speeds; the middle 80% shows the spread without outliers
        // like a pause to re-grip the knob.
        var speeds = intervals.Select(i => 1 / i).Order().ToList();
        var lo = speeds[(int)(speeds.Count * 0.1)];
        var hi = speeds[(int)Math.Min(speeds.Count - 1, speeds.Count * 0.9)];

        return $"{times.Count,5}  {rate,7:0.0}  {rate * 60,9:0}  {sd / mean,10:0%}  {lo:0.0} - {hi:0.0} ticks/s";
    }
}
