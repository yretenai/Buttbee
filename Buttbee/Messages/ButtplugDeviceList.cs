using System.Collections.Generic;

namespace Buttbee.Messages;

public record ButtplugDeviceList : ButtplugMessage {
    public List<ButtplugDeviceAdded> Devices { get; init; } = new();
}
