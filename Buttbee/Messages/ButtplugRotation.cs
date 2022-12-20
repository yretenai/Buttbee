namespace Buttbee.Messages;

public record ButtplugRotation : ButtplugActuator {
    public double Speed { get; set; }
    public bool Clockwise { get; set; }
}
