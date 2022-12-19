namespace Buttbee.Messages;

public record ButtplugServerInfo : ButtplugMessage {
    public string ServerName { get; set; } = null!;
    public uint MessageVersion { get; set; }
    public uint MaxPingTime { get; set; }
}
