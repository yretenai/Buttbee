using System.Collections.Generic;

namespace Buttbee.Messages;

public record ButtplugScalarCmd : ButtplugDeviceMessage {
    public List<ButtplugScalar> Scalars { get; } = new();
}
