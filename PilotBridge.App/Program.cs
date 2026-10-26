using System.Collections.Concurrent;
using System.Diagnostics;
using PilotBridge.App;
using PilotBridge.Core;

// Usage:
//   PilotBridge.App [profile.json] [--monitor] [--seconds N]
//   PilotBridge.App --list
//   PilotBridge.App --calibrate [DialRing|LargeKnob|SmallKnob]
//   PilotBridge.App --replay recording.csv <knob> [profile.json]
// Default profile: profiles\roadcraft.jsonc next to the exe.

var defaultProfile = Path.Combine(AppContext.BaseDirectory, "profiles", "roadcraft.jsonc");

PilotKnob? ParseKnob(string name)
{
    if (Enum.TryParse<PilotKnob>(name, ignoreCase: true, out var knob) && Enum.IsDefined(knob))
        return knob;
    Console.WriteLine($"Unknown knob '{name}'. Use DialRing, LargeKnob or SmallKnob.");
    return null;
}

if (args.Length > 0 && args[0] == "--calibrate")
{
    var knob = ParseKnob(args.Length > 1 ? args[1] : nameof(PilotKnob.DialRing));
    return knob is { } k ? Calibration.Run(k) : 1;
}

if (args.Length > 0 && args[0] == "--replay")
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: --replay recording.csv <knob> [profile.json]");
        return 1;
    }
    var knob = ParseKnob(args[2]);
    return knob is { } k ? Replay.Run(args[1], k, args.Length > 3 ? args[3] : defaultProfile) : 1;
}

var monitor = args.Contains("--monitor");
double? seconds = null;
string? profilePath = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--seconds" && i + 1 < args.Length) seconds = double.Parse(args[++i]);
    else if (!args[i].StartsWith("--")) profilePath = args[i];
}

if (args.Contains("--list"))
{
    var found = PilotDevice.FindVendorInterfaces().ToList();
    Console.WriteLine(found.Count == 0
        ? "Pilot Pro vendor interface not found."
        : string.Join(Environment.NewLine, found.Select(d => $"Found: {d.DevicePath}")));
    return found.Count == 0 ? 1 : 0;
}

profilePath ??= defaultProfile;
Profile profile;
try
{
    profile = Profile.Load(profilePath);
}
catch (Exception e)
{
    Console.WriteLine($"Could not load profile {profilePath}: {e.Message}");
    return 1;
}
Console.WriteLine($"Profile: {profile.Name} ({profilePath})");

using var device = PilotDevice.Open();
if (device is null)
{
    Console.WriteLine("Pilot Pro not found or could not be opened. Plugged in over USB?");
    return 1;
}

VirtualPad pad;
try
{
    pad = new VirtualPad();
}
catch (Exception e)
{
    Console.WriteLine($"Could not create the virtual Xbox 360 pad ({e.Message}). Is ViGEmBus installed?");
    return 1;
}

using (pad)
{
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    if (seconds is { } s) stop.CancelAfter(TimeSpan.FromSeconds(s));

    var clock = Stopwatch.StartNew();
    var reports = new ConcurrentQueue<(double T, PilotReport Report)>();
    string? readError = null;

    // Reports are timestamped as they arrive, on their own thread, so knob
    // tick timing doesn't depend on how often the update loop runs.
    var reader = new Thread(() =>
    {
        try
        {
            while (!stop.IsCancellationRequested)
                reports.Enqueue((clock.Elapsed.TotalSeconds, PilotPro.Parse(device.Read())));
        }
        catch (Exception e) when (!stop.IsCancellationRequested)
        {
            readError = e.Message;
            stop.Cancel();
        }
        catch (Exception)
        {
            // Closing the stream on shutdown ends the blocking read.
        }
    }) { IsBackground = true, Name = "Pilot reader" };
    reader.Start();

    device.SendRawModeCommand();
    Console.WriteLine("Running. Ctrl+C to stop.");

    var engine = new MappingEngine(profile);
    var last = GamepadState.Neutral;
    pad.Send(last);
    var loops = 0;
    var loopRate = 0.0;
    var rateWindowStart = 0.0;
    var nextStatus = 0.0;
    var seenUnknown = new HashSet<string>();

    while (!stop.IsCancellationRequested)
    {
        while (reports.TryDequeue(out var item))
        {
            if (item.Report is RawModeAck)
                Console.WriteLine("Device switched to raw mode.");
            // Once each: the device repeats some of these every second.
            else if (monitor && item.Report is UnknownReport u && seenUnknown.Add(Convert.ToHexString(u.Bytes)))
                Console.WriteLine($"\nUnknown report: {Convert.ToHexString(u.Bytes)}");
            engine.Handle(item.Report, item.T);
        }

        var now = clock.Elapsed.TotalSeconds;
        var state = engine.Update(now);
        if (state != last)
        {
            pad.Send(state);
            last = state;
        }

        loops++;
        if (now - rateWindowStart >= 1)
        {
            loopRate = loops / (now - rateWindowStart);
            loops = 0;
            rateWindowStart = now;
        }
        if (monitor && now >= nextStatus)
        {
            Console.Write($"\rLX {state.LeftX,6:+0.00;-0.00} LY {state.LeftY,6:+0.00;-0.00} " +
                          $"RX {state.RightX,6:+0.00;-0.00} RY {state.RightY,6:+0.00;-0.00} " +
                          $"LT {state.LeftTrigger:0.00} RT {state.RightTrigger:0.00} " +
                          $"[{state.Buttons}]  {loopRate,4:0} Hz        ");
            nextStatus = now + 0.1;
        }

        Thread.Sleep(1);
    }

    pad.Send(GamepadState.Neutral);
    device.Dispose();   // unblocks the reader
    Console.WriteLine();
    Console.WriteLine($"Update loop: {loopRate:0} Hz");
    if (readError is not null)
    {
        Console.WriteLine($"Lost the device: {readError}");
        return 1;
    }
    Console.WriteLine("Stopped.");
}
return 0;
