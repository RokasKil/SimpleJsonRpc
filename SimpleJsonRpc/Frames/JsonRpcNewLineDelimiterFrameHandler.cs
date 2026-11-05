using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace SimpleJsonRpc.Frames;

/// <summary>
/// Splits messages with a simple \n delimiter
/// </summary>
public class JsonRpcNewLineDelimiterFrameHandler : IJsonRpcFrameHandler
{
    private readonly ArrayBufferWriter<byte> _buffer = new();

    private readonly Queue<byte[]> _messages = new();

    public void IngestData(byte[] data, int size)
    {
        int index = 0;
        int delimiterIndex;
        while (index < size && (delimiterIndex = Array.IndexOf(data, (byte)'\n', index, size - index)) >= 0)
        {
            _buffer.Write(new ReadOnlySpan<byte>(data, index, delimiterIndex - index));
            index = delimiterIndex + 1;

            _messages.Enqueue(_buffer.WrittenMemory.ToArray());
            _buffer.Clear();
        }

        if (index < size) _buffer.Write(new ReadOnlySpan<byte>(data, index, size - index));
    }

    public bool TryGetMessage([MaybeNullWhen(false)] out byte[] message) => _messages.TryDequeue(out message);

    public byte[] FrameMessage(byte[] message)
    {
        byte[] result = new byte[message.Length + 1];
        message.CopyTo(result);
        result[message.Length] = (byte)'\n';
        return result;
    }
}