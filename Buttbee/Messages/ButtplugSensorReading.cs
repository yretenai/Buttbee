using System.Text.Json.Serialization;

namespace Buttbee.Messages;

public record ButtplugSensorReading : ButtplugDeviceMessage {
    public uint SensorIndex { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtplugDeviceSensorType SensorType { get; set; }

    public int[] Data { get; set; } = null!;
}
