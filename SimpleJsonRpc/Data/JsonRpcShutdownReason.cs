namespace SimpleJsonRpc.Data;

public record JsonRpcShutdownReason(JsonRpcShutdownReasonEnum Reason, Exception? Exception);