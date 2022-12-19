namespace Buttbee.Messages;

public record ButtplugDeviceAdded : ButtplugDeviceMessage {
    public string DeviceName { get; set; } = null!;
    public uint DeviceMessageGap { get; set; }
    public string? DeviceDisplayName { get; set; }
    public ButtplugDeviceAttributes DeviceMessages { get; set; } = new();
}
