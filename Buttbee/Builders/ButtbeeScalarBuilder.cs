using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Attributes;
using Buttbee.Messages;

namespace Buttbee.Builders;

public class ButtbeeScalarBuilder {
    public ButtbeeScalarBuilder(ButtbeeDevice device) {
        Device = device;
        CanSendNextMessageAt = DateTimeOffset.Now;
    }

    public ButtplugScalarCmd Command { get; } = new();
    public ButtbeeDevice Device { get; }
    public DateTimeOffset CanSendNextMessageAt { get; internal set; }

    public Node Add(string scalarName) {
        var scalar = Device.ScalarActuators.FirstOrDefault(x => x.Name.Equals(scalarName, StringComparison.OrdinalIgnoreCase));
        if (scalar == null) {
            throw new Exception($"Scalar {scalarName} not found on device {Device.Name}");
        }

        return new Node(this, scalar);
    }

    public Node Add(uint index) {
        if (index > (uint) Device.ScalarActuators.Count) {
            throw new Exception($"Scalar index {index} not found on device {Device.Name}");
        }

        return new Node(this, Device.ScalarActuators[(int) index]);
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
        public Node(ButtbeeScalarBuilder scalarBuilder, ButtbeeScalarActuator scalar) {
            Builder = scalarBuilder;
            Scalar = scalar;
            Data = new ButtplugScalar { ActuatorType = scalar.Type, Index = scalar.Id };
        }

        public ButtbeeScalarBuilder Builder { get; }
        public ButtbeeScalarActuator Scalar { get; }
        public ButtplugScalar Data { get; }

        public Node Value(double value) {
            Data.Scalar = Scalar.CalculateSteps(value);
            return this;
        }

        public ButtbeeScalarBuilder Finish() {
            Builder.Command.Scalars.Add(Data);
            if (Scalar.CanSendNextMessageAt > Builder.CanSendNextMessageAt) {
                Builder.CanSendNextMessageAt = Scalar.CanSendNextMessageAt;
            }

            return Builder;
        }
    }
}
