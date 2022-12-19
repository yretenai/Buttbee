using System;

namespace Buttbee.Messages;

[Flags]
public enum ButtplugDeviceActuatorType {
    Unknown = 0,
    Vibrate = 1,
    Rotate = 2,
    Oscillate = 4,
    Constrict = 8,
    Inflate = 16,
    Position = 32,
}
