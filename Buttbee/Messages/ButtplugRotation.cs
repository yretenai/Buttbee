namespace Buttbee.Messages;

public record ButtplugRotation {
    public uint Index { get; init; }
    public double Speed { get; set; }
    public bool Clockwise { get; set; }
}
