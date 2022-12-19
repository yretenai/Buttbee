namespace Buttbee.Messages;

public record ButtplugDeviceMessage : ButtplugMessage {
    public uint DeviceIndex { get; init; }
}

public record ButtplugDeviceRemoved : ButtplugDeviceMessage;

public record ButtplugStopDeviceCmd : ButtplugDeviceMessage;
