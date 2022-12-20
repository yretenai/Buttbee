using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Attributes;
using Buttbee.Messages;

namespace Buttbee.Builders;

public class ButtbeeRotatorBuilder {
    public ButtbeeRotatorBuilder(ButtbeeDevice device) {
        Device = device;
        CanSendNextMessageAt = DateTimeOffset.Now;
    }

    public ButtplugRotateCmd Command { get; } = new();
    public ButtbeeDevice Device { get; }
    public DateTimeOffset CanSendNextMessageAt { get; internal set; }

    public Node CreateNode(string name) {
        var rotator = Device.RotatorActuators.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (rotator == null) {
            throw new Exception($"Rotator {name} not found on device {Device.Name}");
        }

        return new Node(this, rotator);
    }

    public Node CreateNode(uint index) {
        if (index > (uint) Device.RotatorActuators.Count) {
            throw new Exception($"Rotator index {index} not found on device {Device.Name}");
        }

        return new Node(this, Device.RotatorActuators[(int) index]);
    }

    public async Task<ButtbeeDevice> Send(CancellationToken cancellationToken = default) {
        if (CanSendNextMessageAt > DateTimeOffset.Now) {
            await Task.Delay(CanSendNextMessageAt - DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested) {
            return Device;
        }

        await Device.SendImmediate(Command).ConfigureAwait(false);
        return Device;
    }

    public class Node {
        public Node(ButtbeeRotatorBuilder RotatorBuilder, ButtbeeRotatorActuator rotator) {
            Builder = RotatorBuilder;
            Rotator = rotator;
            Data = new ButtplugRotation { Index = rotator.Id };
        }

        public ButtbeeRotatorBuilder Builder { get; }
        public ButtbeeRotatorActuator Rotator { get; }
        public ButtplugRotation Data { get; }

        public Node Speed(double value) {
            Data.Speed = Rotator.CalculateSteps(value);
            return this;
        }

        public Node Clockwise() {
            Data.Clockwise = true;
            return this;
        }

        public Node CounterClockwise() {
            Data.Clockwise = false;
            return this;
        }

        public ButtbeeRotatorBuilder Finish() {
            Builder.Command.Rotations.Add(Data);
            if (Rotator.CanSendNextMessageAt > Builder.CanSendNextMessageAt) {
                Builder.CanSendNextMessageAt = Rotator.CanSendNextMessageAt;
            }

            return Builder;
        }
    }
}
