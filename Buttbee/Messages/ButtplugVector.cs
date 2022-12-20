namespace Buttbee.Messages;

public record ButtplugVector : ButtplugActuator {
    public uint Duration { get; set; }
    public double Position { get; set; }
}
