using System;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Messages;

namespace Buttbee.Attributes;

public class ButtbeeScalarActuator : ButtbeeDeviceActuator {
    public ButtbeeScalarActuator(ButtbeeDevice device, uint id, ButtplugDeviceAttribute attribute) : base(device, id, attribute) {
        Type = attribute.ActuatorType;
        Name = attribute.FeatureDescriptor ?? $"{Type} Actuator {id}";
    }

    public ButtplugDeviceActuatorType Type { get; }
    public double Value { get; set; }

    public async Task Set(double value, CancellationToken cancellationToken = default) {
        if (CanSendNextMessageAt > DateTimeOffset.Now) {
            await Task.Delay(CanSendNextMessageAt - DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested) {
            return;
        }

        Value = CalculateSteps(value);
        CanSendNextMessageAt = DateTimeOffset.Now.AddMilliseconds(Device.Delay);
        await Device.SendImmediate(new ButtplugScalarCmd { DeviceIndex = Device.Id, Scalars = { new ButtplugScalar { Index = Id, Scalar = value, ActuatorType = Type } } });
    }
}
