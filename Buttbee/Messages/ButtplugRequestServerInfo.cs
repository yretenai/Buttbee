namespace Buttbee.Messages;

public record ButtplugRequestServerInfo : ButtplugMessage {
    public string ClientName { get; set; } = "Buttbee/0.1";
    public uint MessageVersion { get; set; } = 3;
}
