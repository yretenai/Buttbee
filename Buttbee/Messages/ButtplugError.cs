namespace Buttbee.Messages;

public record ButtplugError : ButtplugMessage {
    public string ErrorMessage { get; init; } = null!;
    public ButtplugErrorCode ErrorCode { get; set; }
}
