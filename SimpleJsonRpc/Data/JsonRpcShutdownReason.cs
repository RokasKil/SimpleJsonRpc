namespace SimpleJsonRpc.Data;

/// <summary>
/// A structure describing why <see cref="SimpleJsonRpc"/> shutdown.
/// </summary>
/// <param name="Reason">The reason why <see cref="SimpleJsonRpc"/> shutdown.</param>
/// <param name="Exception">The exception that caused the shutdown</param>
public record JsonRpcShutdownReason(JsonRpcShutdownType Reason, Exception? Exception);