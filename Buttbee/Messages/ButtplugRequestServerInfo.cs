namespace Buttbee.Messages;

public record ButtplugRequestServerInfo : ButtplugMessage {
    public string ClientName { get; init; } = "Buttbee/0.5.0";
    public uint MessageVersion { get; init; } = 3;
}
