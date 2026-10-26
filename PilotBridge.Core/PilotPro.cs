namespace PilotBridge.Core;

/// <summary>
/// The XPPen Pilot Pro's vendor-interface protocol, as worked out with
/// tools/probe/probe.py. See CLAUDE.md "Raw mode" for the evidence.
/// </summary>
public static class PilotPro
{
    public const int VendorId = 0x28BD;
    public const int ProductId = 0x0203;
    public const int VendorUsagePage = 0xFF0A;

    /// <summary>Stick range is about -512..+511 with a round gate.</summary>
    public const double StickFullScale = 511.0;

    /// <summary>
    /// Switches the device to raw reporting so every knob tick and button
    /// arrives on the vendor interface. Report ID 0x02 + 19 bytes, the output
    /// report size the vendor interface declares. Same magic Linux hid-uclogic
    /// sends to UGEE v2 tablets; the Pilot Pro acks with 02 B1 04.
    /// </summary>
    public static byte[] RawModeCommand()
    {
        var cmd = new byte[20];
        cmd[0] = 0x02;
        cmd[1] = 0xB0;
        cmd[2] = 0x04;
        return cmd;
    }

    /// <summary>Decodes one input report (report ID byte included).</summary>
    public static PilotReport Parse(ReadOnlySpan<byte> r)
    {
        if (r.Length < 8 || r[0] != 0x02)
            return new UnknownReport(r.ToArray());

        switch (r[1])
        {
            case 0xB1:
                return new RawModeAck();
            case 0xF2:
                // Linux hid-uclogic treats subreport 0xF2 as battery.
                return new BatteryReport(r[3]);
            case 0xF3:
                switch (r[2])
                {
                    case 0x01:
                        return new StickReport(
                            (short)(r[4] | r[5] << 8),
                            (short)(r[6] | r[7] << 8));
                    case 0x02:
                        if (r[3] is 0x01 or 0x02 && Enum.IsDefined((PilotKnob)r[4]))
                            return new KnobTick((PilotKnob)r[4], r[3] == 0x01 ? +1 : -1);
                        break;
                    case 0x03:
                        // 01 = down with the full pressed set in bytes 4-7,
                        // 00 = everything released.
                        var mask = r[3] == 0x01
                            ? (uint)(r[4] | r[5] << 8 | r[6] << 16 | r[7] << 24)
                            : 0u;
                        return new ButtonReport((PilotButton)mask);
                }
                break;
        }
        return new UnknownReport(r.ToArray());
    }
}

public enum PilotKnob : byte
{
    BigKnob = 0x01,
    TopKnob = 0x02,
    OuterRing = 0x04,
}

/// <summary>
/// One bit per control: byte 4 of the button report is bits 0-7, byte 5
/// bits 8-15, and so on. Side button numbers follow Craig's labeled diagram.
/// </summary>
[Flags]
public enum PilotButton : uint
{
    None = 0,

    TouchUp = 0x01u,
    TouchDown = 0x02u,
    TouchRight = 0x04u,
    TouchLeft = 0x08u,
    BigKnobCenter = 0x10u,
    BigKnobPress = 0x20u,

    Side5 = 0x01u << 8,
    Side6 = 0x02u << 8,
    Side8 = 0x04u << 8,
    Side7 = 0x08u << 8,
    Side1 = 0x10u << 8,
    Side2 = 0x20u << 8,
    Side4 = 0x40u << 8,
    Side3 = 0x80u << 8,

    DpadLeft = 0x01u << 16,
    DpadUp = 0x02u << 16,
    DpadRight = 0x04u << 16,
    DpadDown = 0x08u << 16,

    TopKnobPress = 0x01u << 24,
}

public abstract record PilotReport;
public sealed record StickReport(short X, short Y) : PilotReport;
/// <param name="Direction">+1 = clockwise, -1 = counter-clockwise.</param>
public sealed record KnobTick(PilotKnob Knob, int Direction) : PilotReport;
public sealed record ButtonReport(PilotButton Pressed) : PilotReport;
public sealed record BatteryReport(int Percent) : PilotReport;
public sealed record RawModeAck : PilotReport;
public sealed record UnknownReport(byte[] Bytes) : PilotReport;
