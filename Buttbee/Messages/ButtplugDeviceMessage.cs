namespace Buttbee.Messages;

public record ButtplugDeviceMessage : ButtplugMessage {
    public uint DeviceIndex { get; set; }
}

public record ButtplugDeviceRemoved : ButtplugDeviceMessage;

public record ButtplugStopDeviceCmd : ButtplugDeviceMessage;
