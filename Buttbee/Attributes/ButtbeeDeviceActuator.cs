using System;
using Buttbee.Messages;

namespace Buttbee.Attributes;

public abstract class ButtbeeDeviceActuator : ButtbeeDeviceAttribute {
    protected ButtbeeDeviceActuator(ButtbeeDevice device, uint id, ButtplugDeviceAttribute attribute) : base(device, id, attribute) {
        Steps = attribute.StepCount;
        Name = attribute.FeatureDescriptor ?? $"Linear Actuator {id}";
        CanSendNextMessageAt = DateTimeOffset.Now;
    }

    public uint Steps { get; }
    public DateTimeOffset CanSendNextMessageAt { get; protected set; }

    public double CalculateSteps(double value) {
        var stepped = 1d / Steps;
        return Math.Round(value / stepped) * stepped;
    }
}
