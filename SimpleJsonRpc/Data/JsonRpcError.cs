using System.Text.Json.Serialization;

namespace SimpleJsonRpc.Data;

/// <summary>
/// A standard JSON-RPC error structure.
/// </summary>
/// <param name="Code">An error code, can be server defined or can be specification defined, see <see cref="JsonRpcErrorCodes"/>.</param>
/// <param name="Message">An arbitrary status message.</param>
public record JsonRpcError(
    [property: JsonPropertyName("code")]
    int Code,
    [property: JsonPropertyName("message")]
    string Message
);

/// <summary>
/// A standard JSON-RPC error structure with data.
/// </summary>
/// <param name="Code">An error code, can be server defined or can be specification defined, see <see cref="JsonRpcErrorCodes"/>.</param>
/// <param name="Message">An arbitrary status message.</param>
/// <param name="Data">An optional arbitrary data field.</param>
/// <typeparam name="TData">Arbitrary data type.</typeparam>
public record JsonRpcError<TData>(
    int Code,
    string Message,
    [property: JsonPropertyName("data")]
    TData Data
) : JsonRpcError(Code, Message);