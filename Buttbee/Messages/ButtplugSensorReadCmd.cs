using System.Text.Json.Serialization;

namespace Buttbee.Messages;

public record ButtplugSensorReadCmd : ButtplugDeviceMessage {
    public uint SensorIndex { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtplugDeviceSensorType SensorType { get; init; }
}

public record ButtplugSensorSubscribeCmd : ButtplugSensorReadCmd;

public record ButtplugSensorUnsubscribeCmd : ButtplugSensorReadCmd;
