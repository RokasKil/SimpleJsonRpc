using System.Text.Json;

namespace SimpleJsonRpc.Exceptions;

/// <summary>
/// Exception thrown when a server response couldn't be handled.
/// </summary>
/// <param name="response"><see cref="JsonElement"/> representing the received response.</param>
public class JsonRpcInvalidResponseException(JsonElement response) : Exception("Server response was invalid")
{
    /// <summary>
    /// <see cref="JsonElement"/> representing the received response.
    /// </summary>
    public JsonElement Response { get; } = response;
}