namespace SimpleJsonRpc.Data;

/// <summary>
/// An enum describing possible <see cref="SimpleJsonRpc"/> shutdown reasons.
/// </summary>
public enum JsonRpcShutdownType
{
    Disposed,
    ListeningError,
    WritingError
}