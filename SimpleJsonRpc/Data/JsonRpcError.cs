using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SimpleJsonRpc.Data;

public record JsonRpcError(
    [property: JsonPropertyName("code")]
    int Code,
    [property: JsonPropertyName("message")]
    string Message
);

public record JsonRpcError<T>(
    int Code,
    string Message,
    [property: JsonPropertyName("data")]
    T Data
) : JsonRpcError(Code, Message);