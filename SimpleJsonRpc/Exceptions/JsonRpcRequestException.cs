using SimpleJsonRpc.Data;

namespace SimpleJsonRpc.Exceptions;

/// <summary>
/// Exception a server method should throw when responding with an error.
/// </summary>
/// <param name="error">The error that will be sent back to the client.</param>
public class JsonRpcRequestException(JsonRpcError error) : Exception(error.Message)
{
    /// <summary>
    /// The error that will be sent back to the client.
    /// </summary>
    public JsonRpcError Error { get; } = error;
}