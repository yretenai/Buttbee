using System;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Messages;

namespace Buttbee.Attributes;

public class ButtbeeRotatorActuator : ButtbeeDeviceActuator {
    public ButtbeeRotatorActuator(ButtbeeDevice device, uint id, ButtplugDeviceAttribute attribute) : base(device, id, attribute) => Name = attribute.FeatureDescriptor ?? $"Rotator Actuator {id}";

    public double Speed { get; set; }
    public bool Clockwise { get; set; }

    public async Task Set(double speed, bool clockwise, CancellationToken cancellationToken = default) {
        if (CanSendNextMessageAt > DateTimeOffset.Now) {
            await Task.Delay(CanSendNextMessageAt - DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested) {
            return;
        }

        Speed = CalculateSteps(speed);
        Clockwise = clockwise;
        CanSendNextMessageAt = DateTimeOffset.Now.AddMilliseconds(Device.Delay);
        await Device.SendImmediate(new ButtplugRotateCmd { DeviceIndex = Device.Id, Rotations = { new ButtplugRotation { Index = Id, Speed = speed, Clockwise = clockwise } } });
    }
}
