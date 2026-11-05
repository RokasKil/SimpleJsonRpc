using System.Text.Json.Serialization;

namespace SimpleJsonRpc.Data;

public record JsonRpcError(
    [property: JsonPropertyName("code")]
    int Code,
    [property: JsonPropertyName("message")]
    string Message
);

public record JsonRpcError<TData>(
    int Code,
    string Message,
    [property: JsonPropertyName("data")]
    TData Data
) : JsonRpcError(Code, Message);