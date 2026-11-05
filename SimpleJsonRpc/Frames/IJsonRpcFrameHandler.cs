using System.Diagnostics.CodeAnalysis;

namespace SimpleJsonRpc.Frames;

/// <summary>
/// An interface for <see cref="JsonRpc"/> transimission frame handlers.
/// </summary>
public interface IJsonRpcFrameHandler
{
    /// <summary>
    /// Ingest data and internally produce messages.
    /// </summary>
    /// <param name="data">Byte Span to ingest.</param>
    void IngestData(ReadOnlySpan<byte> data);

    /// <summary>
    /// Try to get a produced message in FIFO order.
    /// </summary>
    /// <param name="message">The retrieved message.</param>
    /// <returns>true if a message was retrieved.</returns>
    bool TryGetMessage([MaybeNullWhen(false)] out byte[] message);

    /// <summary>
    /// Prepare a message for transmission by framing it.
    /// </summary>
    /// <param name="message">The message to frame</param>
    byte[] FrameMessage(byte[] message);
}