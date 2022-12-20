using System;
using Buttbee.Attributes;

namespace Buttbee.Events;

public class ButtbeeSensorEventArgs : EventArgs {
    public ButtbeeSensorEventArgs(ButtbeeDeviceSensor sensor, int[] values) {
        Sensor = sensor;
        Values = values;
    }

    public ButtbeeDeviceSensor Sensor { get; init; }
    public int[] Values { get; init; }
}
