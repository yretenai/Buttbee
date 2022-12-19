using System.Collections.Generic;

namespace Buttbee.Messages;

public record ButtplugDeviceAttributes {
    // todo: split each attribute into a unique attribute class
    public List<ButtplugDeviceAttribute> ScalarCmd { get; set; } = new();
    public List<ButtplugDeviceAttribute> RotateCmd { get; set; } = new();
    public List<ButtplugDeviceAttribute> LinearCmd { get; set; } = new();
    public List<ButtplugDeviceAttribute> SensorReadCmd { get; set; } = new();
    public List<ButtplugDeviceAttribute> RawReadCmd { get; set; } = new();
    public List<ButtplugDeviceAttribute> RawWriteCmd { get; set; } = new();
    public List<ButtplugDeviceAttribute> RawSubscribeCmd { get; set; } = new();
}
