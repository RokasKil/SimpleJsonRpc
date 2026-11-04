namespace SimpleJsonRpc.Data;

internal class JsonRpcMethod
{
    public required string Name { get; init; }
    public required Delegate Delegate { get; init; }
    public Type? ParamsType { get; init; }
}