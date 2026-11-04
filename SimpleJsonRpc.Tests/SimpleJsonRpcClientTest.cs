using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Nerdbank.Streams;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Exceptions;
using SimpleJsonRpc.Tests.Data;
using Xunit.Abstractions;

namespace SimpleJsonRpc.Tests;

[SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
public class SimpleJsonRpcClientTest(ITestOutputHelper log)
{
    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    private async Task<T> ExpectedClientInputAndOutputTest<T>(string input, string output, Func<SimpleJsonRpc, T> clientRpcMethodAction, Action<SimpleJsonRpc>? prepareRpcAction = null) where T : Task
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] outputBytes = Encoding.UTF8.GetBytes(output);
        byte[] readBuffer = new byte[Math.Max(4000, inputBytes.Length)];
        ArrayBufferWriter<byte> buffer = new();
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        SimpleJsonRpc rpc = new(clientStream);
        prepareRpcAction?.Invoke(rpc);
        rpc.StartListening();
        T rpcTask = clientRpcMethodAction.Invoke(rpc);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            while (!cts.IsCancellationRequested)
            {
                int read = await serverStream.ReadAsync(readBuffer, cts.Token);
                buffer.Write(new ReadOnlySpan<byte>(readBuffer, 0, read));
                if (outputBytes.Length != 0 && buffer.WrittenCount == inputBytes.Length) break;
            }
        }
        catch (OperationCanceledException)
        {
        }

        string recievedLine = Encoding.UTF8.GetString(buffer.WrittenSpan);
        log.WriteLine($"Input Expected: '{input.ReplaceLineEndings("\\n")}'");
        log.WriteLine($"Input Got:      '{recievedLine.ReplaceLineEndings("\\n")}'");
        Assert.Equal(input, recievedLine);
        if (outputBytes.Length != 0)
        {
            await serverStream.WriteAsync(outputBytes);
            await serverStream.FlushAsync();
        }

        await rpcTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await clientStream.DisposeAsync();
        Assert.Equal(0, await serverStream.ReadAsync(readBuffer));
        return rpcTask;
    }

    [Fact]
    public async Task SimpleJsonRpcClient_RequestWithoutParams_TaskReturnsResult()
    {
        TwoNumbers result = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":0}\n",
            "{\"jsonrpc\":\"2.0\",\"result\":{\"A\":1,\"B\":3},\"id\":0}\n",
            rpc => Task.Run(() => rpc.Request<TwoNumbers>("method"))
        ).Result;
        Assert.Equal(new(1, 3), result);
    }

    [Fact]
    public async Task SimpleJsonRpcClient_RequestWithParams_TaskReturnsResult()
    {
        TwoNumbers result = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"params\":{\"A\":5,\"B\":9},\"id\":0}\n",
            "{\"jsonrpc\":\"2.0\",\"result\":{\"A\":1,\"B\":3},\"id\":0}\n",
            rpc => Task.Run(() => rpc.Request<TwoNumbers>("method", new TwoNumbers(5, 9)))
        ).Result;
        Assert.Equal(new(1, 3), result);
    }

    [Fact]
    public async Task SimpleJsonRpcClient_MultipleRequestsWithShuffledRespones_TaskReturnsResult()
    {
        int[] result = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":0}\n" +
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":1}\n" +
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":2}\n",
            "{\"jsonrpc\":\"2.0\",\"result\":2,\"id\":2}\n" +
            "{\"jsonrpc\":\"2.0\",\"result\":0,\"id\":0}\n" +
            "{\"jsonrpc\":\"2.0\",\"result\":1,\"id\":1}\n",
            rpc => Task.Run(() => Task.WhenAll(
                rpc.Request<int>("method"),
                rpc.Request<int>("method"),
                rpc.Request<int>("method")))
        ).Result;
        for (var i = 0; i < result.Length; i++)
            Assert.Equal(i, result[i]);
    }

    [Fact]
    public async Task SimpleJsonRpcClient_RequestWithUnexpectedResponseType_TaskFaultsWithJsonException()
    {
        Task<int> resultTask = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":0}\n",
            "{\"jsonrpc\":\"2.0\",\"result\":\"text\",\"id\":0}\n",
            rpc => Task.Run(() => rpc.Request<int>("method"))
        );
        Assert.True(resultTask.IsFaulted);
        Assert.Equal(typeof(JsonException), resultTask.Exception.GetBaseException().GetType());
    }

    [Fact]
    public async Task SimpleJsonRpcClient_RequestWithErrorResult_TaskFaultsWithForwardedError()
    {
        Task<int> resultTask = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":0}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":1,\"message\":\"error\"},\"id\":0}\n",
            rpc => Task.Run(() => rpc.Request<int>("method"))
        );
        Assert.True(resultTask.IsFaulted);
        Assert.Equal(typeof(JsonRpcResponseException), resultTask.Exception.GetBaseException().GetType());
        Assert.Equal(new(1, "error", null), ((JsonRpcResponseException)resultTask.Exception.GetBaseException()).Error);
    }

    [Fact]
    public async Task SimpleJsonRpcClient_RequestWithErrorWithDataResult_TaskFaultsWithForwardedError()
    {
        Task<int> resultTask = await ExpectedClientInputAndOutputTest
        (
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":0}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":1,\"message\":\"error\",\"data\":9},\"id\":0}\n",
            rpc => Task.Run(() => rpc.Request<int>("method"))
        );
        Assert.True(resultTask.IsFaulted);
        Assert.Equal(typeof(JsonRpcResponseException), resultTask.Exception.GetBaseException().GetType());
        var error = ((JsonRpcResponseException)resultTask.Exception.GetBaseException()).Error;
        Assert.Equal(1, error.Code);
        Assert.Equal("error", error.Message);
        Assert.True(error.Data.HasValue);
        Assert.Equal(JsonValueKind.Number, error.Data.Value.ValueKind);
        Assert.Equal(9, error.Data.Value.GetInt32());
    }

    [Fact]
    public async Task SimpleJsonRpcClient_RequestWithInvalidErrorResult_TaskFaultsWithForwardedError()
    {
        Task<int> resultTask = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":0}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":\"ERROR\"},\"id\":0}\n",
            rpc => Task.Run(() => rpc.Request<int>("method"))
        );
        Assert.True(resultTask.IsFaulted);
        Assert.Equal(typeof(JsonRpcInvalidResponseException), resultTask.Exception.GetBaseException().GetType());
        Assert.Equal("{\"jsonrpc\":\"2.0\",\"error\":{\"code\":\"ERROR\"},\"id\":0}", ((JsonRpcInvalidResponseException)resultTask.Exception.GetBaseException()).Response.ToString());
    }

    [Fact]
    public async Task SimpleJsonRpcClient_Notify_RequestGetsSent()
    {
        Task resultTask = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\"}\n",
            "",
            rpc => Task.Run(() => rpc.Notify("method"))
        );
        Assert.True(resultTask.IsCompleted);
    }

    [Fact]
    public async Task SimpleJsonRpcClient_NotifyWithParams_RequestGetsSent()
    {
        Task resultTask = await ExpectedClientInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"params\":{\"A\":1,\"B\":5}}\n",
            "",
            rpc => Task.Run(() => rpc.Notify("method", new TwoNumbers(1, 5)))
        );
        Assert.True(resultTask.IsCompleted);
    }

    [Fact]
    public async Task SimpleJsonRpcClient_NotificationWithUnserializableError_TaskFaultsWithNotSupportedException()
    {
        Task resultTask = await ExpectedClientInputAndOutputTest(
            "",
            "",
            rpc => Task.Run(() => rpc.Notify("method", typeof(string)))
        );
        Assert.True(resultTask.IsFaulted);
        Assert.Equal(typeof(NotSupportedException), resultTask.Exception.GetBaseException().GetType());
    }
}