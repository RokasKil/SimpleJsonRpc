namespace SimpleJsonRpc.Data;

internal record JsonRpcMethod(string Name, Delegate Delegate, Type? ParamsType);