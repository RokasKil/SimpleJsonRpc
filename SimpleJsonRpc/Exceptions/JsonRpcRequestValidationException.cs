namespace SimpleJsonRpc.Exceptions;

public class JsonRpcRequestValidationException(string? message, int errorCode) : Exception(message)
{
    public int ErrorCode { get; init; } = errorCode;
}