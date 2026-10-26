using PilotBridge.Core;

namespace PilotBridge.Core.Tests;

/// <summary>Byte strings are real reports captured from the device with probe.py.</summary>
public class PilotProParseTests
{
    private static byte[] Hex(string s) => s.Split(' ').Select(b => Convert.ToByte(b, 16)).ToArray();

    [Theory]
    [InlineData("02 F3 01 01 C4 FF 64 FF 00 00 00 00", -60, -156)]
    [InlineData("02 F3 01 01 FE 01 00 00 00 00 00 00", 510, 0)]      // full right
    [InlineData("02 F3 01 01 00 00 FF 01 00 00 00 00", 0, 511)]      // full up
    [InlineData("02 F3 01 01 00 00 00 00 00 00 00 00", 0, 0)]        // released
    public void Stick_IsSigned16BitLittleEndian(string hex, int x, int y)
    {
        Assert.Equal(new StickReport((short)x, (short)y), PilotPro.Parse(Hex(hex)));
    }

    [Theory]
    [InlineData("02 F3 02 01 01 01 00 00 00 00 00 00", PilotKnob.LargeKnob, +1)]
    [InlineData("02 F3 02 02 01 01 00 00 00 00 00 00", PilotKnob.LargeKnob, -1)]
    [InlineData("02 F3 02 01 04 01 00 00 00 00 00 00", PilotKnob.DialRing, +1)]
    [InlineData("02 F3 02 02 02 01 00 00 00 00 00 00", PilotKnob.SmallKnob, -1)]
    public void Knob_Dir01IsClockwise(string hex, PilotKnob knob, int direction)
    {
        Assert.Equal(new KnobTick(knob, direction), PilotPro.Parse(Hex(hex)));
    }

    [Theory]
    [InlineData("02 F3 03 01 00 10 00 00 00 00 00 00", PilotButton.Side1)]
    [InlineData("02 F3 03 01 00 80 00 00 00 00 00 00", PilotButton.Side3)]
    [InlineData("02 F3 03 01 00 04 00 00 00 00 00 00", PilotButton.Side8)]
    [InlineData("02 F3 03 01 00 00 02 00 00 00 00 00", PilotButton.DpadUp)]
    [InlineData("02 F3 03 01 00 00 00 01 00 00 00 00", PilotButton.SmallKnobPress)]
    [InlineData("02 F3 03 01 20 00 00 00 00 00 00 00", PilotButton.LargeKnobPress)]
    [InlineData("02 F3 03 01 08 00 00 00 00 00 00 00", PilotButton.TouchLeft)]
    [InlineData("02 F3 03 00 00 00 00 00 00 00 00 00", PilotButton.None)]   // release
    public void Buttons_OneBitPerControl(string hex, PilotButton expected)
    {
        Assert.Equal(new ButtonReport(expected), PilotPro.Parse(Hex(hex)));
    }

    [Fact]
    public void BatteryAndAck_AreRecognized()
    {
        Assert.Equal(new BatteryReport(100), PilotPro.Parse(Hex("02 F2 01 64 01 50 00 00 00 00 00 00")));
        Assert.IsType<RawModeAck>(PilotPro.Parse(Hex("02 B1 04 00 00 00 00 00 00 00 00 00")));
    }

    [Theory]
    [InlineData("02 B4 05 01 02 01 00 00 00 00 00 00")]   // seen only with the XPPen app running
    [InlineData("02 F3 02 01 07 01 00 00 00 00 00 00")]   // knob id that doesn't exist
    [InlineData("01 02 03")]                              // too short
    public void Unrecognized_IsUnknownNotAnException(string hex)
    {
        Assert.IsType<UnknownReport>(PilotPro.Parse(Hex(hex)));
    }

    [Fact]
    public void RawModeCommand_IsReportId2PlusB004PaddedTo20()
    {
        var cmd = PilotPro.RawModeCommand();
        Assert.Equal(20, cmd.Length);
        Assert.Equal(new byte[] { 0x02, 0xB0, 0x04 }, cmd[..3]);
        Assert.All(cmd[3..], b => Assert.Equal(0, b));
    }
}
