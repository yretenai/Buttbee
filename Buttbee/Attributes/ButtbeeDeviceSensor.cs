using System;
using System.Linq;
using System.Threading.Tasks;
using Buttbee.Events;
using Buttbee.Messages;

namespace Buttbee.Attributes;

public class ButtbeeDeviceSensor : ButtbeeDeviceAttribute {
    public ButtbeeDeviceSensor(ButtbeeDevice device, uint id, ButtplugDeviceAttribute attribute) : base(device, id, attribute) {
        Name = attribute.FeatureDescriptor ?? $"Sensor {id}";
        Type = attribute.SensorType;
        Ranges = attribute.SensorRange?.Select(x => new Range(x[0], x[1])).ToArray() ?? Array.Empty<Range>();
        Values = new int[Ranges.Length];
        Array.Fill(Values, int.MinValue);
    }

    public ButtplugDeviceSensorType Type { get; set; }
    public Range[] Ranges { get; set; }
    public int[] Values { get; set; }

    public event EventHandler<ButtbeeSensorEventArgs>? ValueChanged;

    public void Update(int[] data) {
        if (data.Length != Values.Length) {
            throw new ArgumentException("Invalid data length");
        }

        var hasChanged = false;
        for (var i = 0; i < data.Length; i++) {
            if (Values[i] != data[i]) {
                Values[i] = data[i];
                hasChanged = true;
            }
        }

        if (hasChanged) {
            ValueChanged?.Invoke(Device, new ButtbeeSensorEventArgs(this, data));
        }
    }

    public async Task<int[]> Poll() {
        var (msg, err, _) = await Device.SendImmediate<ButtplugSensorReading, ButtplugSensorReadCmd>(new ButtplugSensorReadCmd { SensorIndex = Id, SensorType = Type }).ConfigureAwait(false);
        if (err is not null) {
            throw new ButtbeeException(err);
        }

        Update(msg!.Data);

        return msg.Data;
    }

    public async Task Subscribe() {
        await Device.SendImmediate(new ButtplugSensorSubscribeCmd { SensorIndex = Id, SensorType = Type }).ConfigureAwait(false);
    }

    public async Task Unsubscribe() {
        await Device.SendImmediate(new ButtplugSensorUnsubscribeCmd { SensorIndex = Id, SensorType = Type }).ConfigureAwait(false);
    }
}
