using System.Text.Json;

namespace SimpleJsonRpc.Exceptions;

public class JsonRpcInvalidResponseException(JsonElement response) : Exception("Server response was invalid")
{
    public JsonElement Response { get; } = response;
}