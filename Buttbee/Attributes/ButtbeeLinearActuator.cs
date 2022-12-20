using System;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Messages;

namespace Buttbee.Attributes;

public class ButtbeeLinearActuator : ButtbeeDeviceActuator {
    public ButtbeeLinearActuator(ButtbeeDevice device, uint id, ButtplugDeviceAttribute attribute) : base(device, id, attribute) => Name = attribute.FeatureDescriptor ?? $"Linear Actuator {id}";

    public double Position { get; set; }
    public uint Duration { get; set; }

    public async Task Set(double position, uint duration, CancellationToken cancellationToken = default) {
        if (CanSendNextMessageAt > DateTimeOffset.Now) {
            await Task.Delay(CanSendNextMessageAt - DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested) {
            return;
        }

        Position = CalculateSteps(position);
        Duration = duration;
        CanSendNextMessageAt = DateTimeOffset.Now.AddMilliseconds(Device.Delay);
        await Device.SendImmediate(new ButtplugLinearCmd { DeviceIndex = Device.Id, Vectors = { new ButtplugVector { Index = Id, Position = position, Duration = duration } } });
    }
}
