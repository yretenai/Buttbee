namespace Buttbee.Messages;

public record ButtplugError : ButtplugMessage {
    public string ErrorMessage { get; set; } = null!;
    public ButtplugErrorCode ErrorCode { get; set; }
}
