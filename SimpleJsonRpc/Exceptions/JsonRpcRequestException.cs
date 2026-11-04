using SimpleJsonRpc.Data;

namespace SimpleJsonRpc.Exceptions;

public class JsonRpcRequestException(JsonRpcError error) : Exception(error.Message)
{
    public JsonRpcError Error { get; } = error;
}