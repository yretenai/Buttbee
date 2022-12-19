namespace Buttbee.Messages;

public record ButtplugMessage {
    public uint Id { get; set; }
}

public record ButtplugOk : ButtplugMessage;

public record ButtplugPing : ButtplugMessage;

public record ButtplugRequestDeviceList : ButtplugMessage;

public record ButtplugScanningFinished : ButtplugMessage;

public record ButtplugStartScanning : ButtplugMessage;

public record ButtplugStopAllDevices : ButtplugMessage;

public record ButtplugStopScanning : ButtplugMessage;
