using System;

namespace Buttbee.Messages;

[Flags]
public enum ButtplugDeviceSensorType {
    Unknown = 0,
    Battery = 1,
    RSSI = 2,
    Button = 4,
    Pressure = 8,
}
