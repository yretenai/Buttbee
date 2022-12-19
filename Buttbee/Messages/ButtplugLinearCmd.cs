using System.Collections.Generic;

namespace Buttbee.Messages;

public record ButtplugLinearCmd : ButtplugDeviceMessage {
    public List<ButtplugVector> Vectors { get; } = new();
}
