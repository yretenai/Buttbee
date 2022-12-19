namespace Buttbee.Messages;

public record ButtplugVector {
    public uint Index { get; init; }
    public uint Duration { get; set; }
    public double Position { get; set; }
}
