using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace SimpleJsonRpc.Frames;

/// <summary>
/// Splits messages with a simple \n delimiter, default maxMessageSize is 10 MiB
/// </summary>
public class JsonRpcNewLineDelimiterFrameHandler(int maxMessageSize = 10 * 1024 * 1024) : IJsonRpcFrameHandler
{
    private readonly ArrayBufferWriter<byte> _buffer = new();

    private readonly Queue<byte[]> _messages = new();

    /// <inheritdoc/>
    public void IngestData(ReadOnlySpan<byte> data)
    {
        int delimiterIndex;
        while (!data.IsEmpty && (delimiterIndex = data.IndexOf((byte)'\n')) >= 0)
        {
            _buffer.Write(data.Slice(0, delimiterIndex));

            data = data.Slice(delimiterIndex + 1);
            if (_buffer.WrittenCount != 0) _messages.Enqueue(_buffer.WrittenMemory.ToArray());
            _buffer.ResetWrittenCount();
        }

        if (!data.IsEmpty) _buffer.Write(data);
        if (_buffer.WrittenCount > maxMessageSize) throw new InvalidOperationException($"Message too large: {_buffer.WrittenMemory.Length}");
    }

    /// <inheritdoc/>
    public bool TryGetMessage([MaybeNullWhen(false)] out byte[] message) => _messages.TryDequeue(out message);

    /// <inheritdoc/>
    public byte[] FrameMessage(byte[] message)
    {
        byte[] result = new byte[message.Length + 1];
        message.CopyTo(result, 0);
        result[message.Length] = (byte)'\n';
        return result;
    }
}