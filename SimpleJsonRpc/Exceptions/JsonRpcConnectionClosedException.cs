using System.Text.Json;
using SimpleJsonRpc.Data;

namespace SimpleJsonRpc.Exceptions;

/// <summary>
/// Exception thrown for all pending requests when the server shutdowns.
/// </summary>
/// <param name="reason"> The reason why the server shutdown.</param>
public class JsonRpcConnectionClosedException(JsonRpcShutdownReason reason) : Exception($"JSON-RPC connection closed: {reason.Reason}", reason.Exception)
{
    /// <summary>
    /// The reason why the server shutdown.
    /// </summary>
    public JsonRpcShutdownReason Reason { get; } = reason;
}