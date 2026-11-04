using System.Text.Json;
using SimpleJsonRpc.Data;

namespace SimpleJsonRpc.Exceptions;

public class JsonRpcResponseException(JsonRpcError<JsonElement?> error) : Exception(error.Message)
{
    public JsonRpcError<JsonElement?> Error { get; } = error;
}