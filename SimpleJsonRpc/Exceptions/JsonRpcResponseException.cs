using System.Text.Json;
using SimpleJsonRpc.Data;

namespace SimpleJsonRpc.Exceptions;

/// <summary>
/// Exception that's thrown when the server responds with an error.
/// </summary>
/// <param name="error">The error that the server sent as a response.</param>
public class JsonRpcResponseException(JsonRpcError<JsonElement?> error) : Exception(error.Message)
{
    public JsonRpcError<JsonElement?> Error { get; } = error;
}