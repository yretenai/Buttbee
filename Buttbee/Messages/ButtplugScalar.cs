using System.Text.Json.Serialization;

namespace Buttbee.Messages;

public record ButtplugScalar {
    public uint Index { get; init; }
    public double Scalar { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtplugDeviceActuatorType ActuatorType { get; init; }
}
