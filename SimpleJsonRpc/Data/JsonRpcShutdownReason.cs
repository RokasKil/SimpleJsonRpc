namespace SimpleJsonRpc.Data;

/// <summary>
/// A structure describing why <see cref="JsonRpc"/> shutdown.
/// </summary>
/// <param name="Reason">The reason why <see cref="JsonRpc"/> shutdown.</param>
/// <param name="Exception">The exception that caused the shutdown</param>
public record JsonRpcShutdownReason(JsonRpcShutdownType Reason, Exception? Exception);