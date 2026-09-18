using System;

namespace Server.Exceptions;

public sealed class InvalidParameterException : Exception {
    public InvalidParameterException() {
    }

    public InvalidParameterException(string message) : base(message) {
    }
}
