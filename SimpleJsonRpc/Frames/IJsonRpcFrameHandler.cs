using System.Diagnostics.CodeAnalysis;

namespace SimpleJsonRpc.Frames;

public interface IJsonRpcFrameHandler
{
    /// <summary>
    /// Ingest data and internally produce messages.
    /// </summary>
    /// <param name="data">Byte array to ingest.</param>
    /// <param name="size">The amount of bytes to ingest.</param>
    void IngestData(byte[] data, int size);

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