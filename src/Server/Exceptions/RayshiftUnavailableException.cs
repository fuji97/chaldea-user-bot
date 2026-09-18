using System;

namespace Server.Exceptions;

public sealed class RayshiftUnavailableException : Exception {
    public RayshiftUnavailableException() {
    }

    public RayshiftUnavailableException(string message) : base(message) {
    }

    public RayshiftUnavailableException(string message, Exception innerException) : base(message, innerException) {
    }
}
