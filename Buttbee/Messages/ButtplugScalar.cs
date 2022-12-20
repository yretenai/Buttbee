using System.Text.Json.Serialization;

namespace Buttbee.Messages;

public record ButtplugScalar : ButtplugActuator {
    public double Scalar { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ButtplugDeviceActuatorType ActuatorType { get; init; }
}
