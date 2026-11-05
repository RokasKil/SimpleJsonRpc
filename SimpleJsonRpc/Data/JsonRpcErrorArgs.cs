using System.Text.Json;

namespace SimpleJsonRpc.Data;

/// <summary>
/// Event arguments for <see cref="JsonRpc.Error"/>
/// </summary>
/// <param name="type">Error type</param>
/// <param name="element">The associated <see cref="JsonElement"/>, if available.</param>
/// <param name="methodName">The associated method name, if available.</param>
/// <param name="exception">The associated exception, if available</param>
public class JsonRpcErrorEventArgs(JsonRpcErrorType type, JsonElement? element = null, string? methodName = null, Exception? exception = null) : EventArgs
{
    /// <summary>
    /// Error type.
    /// </summary>
    public JsonRpcErrorType Type { get; } = type;
    /// <summary>
    /// The associated <see cref="JsonElement"/>, if available.
    /// </summary>
    public JsonElement? Element { get; } = element;
    /// <summary>
    /// The associated method name, if available.
    /// </summary>
    public string? MethodName { get; } = methodName;
    /// <summary>
    /// The associated exception, if available.
    /// </summary>
    public Exception? Exception { get; } = exception;
}