using System;
using Buttbee.Messages;

namespace Buttbee;

public class ButtbeeException : Exception {
    public ButtbeeException(ButtplugError error) : base(error.ErrorMessage) => Error = error;

    public ButtbeeException(ButtplugError error, Exception innerException) : base(error.ErrorMessage, innerException) => Error = error;

    public ButtbeeException() { }

    public ButtbeeException(string message) : base(message) => Error = new ButtplugError { ErrorMessage = message };

    public ButtbeeException(string message, Exception innerException) : base(message, innerException) => Error = new ButtplugError { ErrorMessage = message };

    public ButtplugError? Error { get; }
}
