using System;

namespace Buttbee.Events;

public class ButtbeeDeviceEventArgs : EventArgs {
    public ButtbeeDeviceEventArgs(ButtbeeDevice device) => Device = device;

    public ButtbeeDevice Device { get; init; }
}
