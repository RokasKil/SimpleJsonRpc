namespace SimpleJsonRpc.Data;

/// <summary>
/// An enum describing possible <see cref="JsonRpc"/> shutdown reasons.
/// </summary>
public enum JsonRpcShutdownType
{
    /// <summary>
    /// Shutdown due to a call to <see cref="JsonRpc.DisposeAsync()"/>.
    /// </summary>
    Disposed,
    /// <summary>
    /// Shutdown due to an error when attempting to listen for messages.
    /// </summary>
    ListeningError,
    /// <summary>
    /// Read stream encountered an EOF.
    /// </summary>
    RemoteClosed,
    /// <summary>
    /// Shutdown due to an error when attempting to write a message.
    /// </summary>
    WritingError
}