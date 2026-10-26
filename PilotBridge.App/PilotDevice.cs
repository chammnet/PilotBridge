using HidSharp;
using PilotBridge.Core;

namespace PilotBridge.App;

/// <summary>The Pilot Pro's vendor interface: the only one Windows lets us read.</summary>
internal sealed class PilotDevice : IDisposable
{
    private readonly HidStream _stream;
    private readonly byte[] _buffer;

    private PilotDevice(HidDevice device, HidStream stream)
    {
        _stream = stream;
        _stream.ReadTimeout = Timeout.Infinite;
        _buffer = new byte[device.GetMaxInputReportLength()];
    }

    public static IEnumerable<HidDevice> FindVendorInterfaces() =>
        DeviceList.Local.GetHidDevices(PilotPro.VendorId, PilotPro.ProductId)
            .Where(IsVendorInterface);

    private static bool IsVendorInterface(HidDevice d)
    {
        try
        {
            return d.GetReportDescriptor().DeviceItems
                .SelectMany(i => i.Usages.GetAllValues())
                .Any(u => u >> 16 == PilotPro.VendorUsagePage);
        }
        catch (Exception)
        {
            // Windows won't hand out some collections' descriptors; those
            // aren't the vendor interface anyway.
            return false;
        }
    }

    public static PilotDevice? Open()
    {
        var device = FindVendorInterfaces().FirstOrDefault();
        if (device is null || !device.TryOpen(out var stream))
            return null;
        return new PilotDevice(device, stream);
    }

    public void SendRawModeCommand() => _stream.Write(PilotPro.RawModeCommand());

    /// <summary>Blocks until a report arrives. Throws IOException if the device goes away.</summary>
    public byte[] Read()
    {
        var n = _stream.Read(_buffer, 0, _buffer.Length);
        return _buffer[..n];
    }

    public void Dispose() => _stream.Dispose();
}
