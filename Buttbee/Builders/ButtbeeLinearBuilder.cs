using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Attributes;
using Buttbee.Messages;

namespace Buttbee.Builders;

public class ButtbeeLinearBuilder {
    public ButtbeeLinearBuilder(ButtbeeDevice device) {
        Device = device;
        CanSendNextMessageAt = DateTimeOffset.Now;
    }

    public ButtplugLinearCmd Command { get; } = new();
    public ButtbeeDevice Device { get; }
    public DateTimeOffset CanSendNextMessageAt { get; internal set; }

    public Node Add(string name) {
        var linear = Device.LinearActuators.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (linear == null) {
            throw new Exception($"Linear {name} not found on device {Device.Name}");
        }

        return new Node(this, linear);
    }

    public Node Add(uint index) {
        if (index > (uint) Device.LinearActuators.Count) {
            throw new Exception($"Linear index {index} not found on device {Device.Name}");
        }

        return new Node(this, Device.LinearActuators[(int) index]);
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
        public Node(ButtbeeLinearBuilder linearBuilder, ButtbeeLinearActuator linear) {
            Builder = linearBuilder;
            Linear = linear;
            Data = new ButtplugVector { Index = linear.Id };
        }

        public ButtbeeLinearBuilder Builder { get; }
        public ButtbeeLinearActuator Linear { get; }
        public ButtplugVector Data { get; }

        public Node Position(double value) {
            Data.Position = Linear.CalculateSteps(value);
            return this;
        }

        public Node Duration(uint value) {
            Data.Duration = value;
            return this;
        }

        public Node Duration(TimeSpan value) {
            Data.Duration = (uint) value.TotalMilliseconds;
            return this;
        }

        public ButtbeeLinearBuilder Finish() {
            Builder.Command.Vectors.Add(Data);
            if (Linear.CanSendNextMessageAt > Builder.CanSendNextMessageAt) {
                Builder.CanSendNextMessageAt = Linear.CanSendNextMessageAt;
            }

            return Builder;
        }
    }
}
