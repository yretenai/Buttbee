using System.Collections.Generic;

namespace Buttbee.Messages;

public record ButtplugRotateCmd : ButtplugDeviceMessage {
    public List<ButtplugRotation> Rotations { get; } = new();
}
