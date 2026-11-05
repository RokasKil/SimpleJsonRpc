using SimpleJsonRpc.Data;

namespace SimpleJsonRpc.Exceptions;

/// <summary>
/// Exception a server method should throw when responding with an error.
/// </summary>
/// <param name="error">The error to send back to the client.</param>
public class JsonRpcRequestException(JsonRpcError error) : Exception(error.Message)
{
    public JsonRpcError Error { get; } = error;
}