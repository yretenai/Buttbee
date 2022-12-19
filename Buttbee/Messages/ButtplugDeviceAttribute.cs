using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Buttbee.Messages;

public record ButtplugDeviceAttribute {
    public string? FeatureDescriptor { get; set; }
    public uint StepCount { get; set; }
    public List<int[]>? SensorRange { get; set; }
    public List<string>? Endpoints { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtplugDeviceActuatorType ActuatorType { get; set; } = ButtplugDeviceActuatorType.Unknown;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtplugDeviceSensorType SensorType { get; set; } = ButtplugDeviceSensorType.Unknown;
}
