using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nerdbank.Streams;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Exceptions;
using SimpleJsonRpc.Tests.Data;
using Xunit.Abstractions;

namespace SimpleJsonRpc.Tests;

public class SimpleJsonRpcServerTest(ITestOutputHelper log)
{
    private async Task ExpectedServerInputAndOutputTest(string input, string output, Action<SimpleJsonRpc>? prepareRpcAction = null)
    {
        await ExpectedServerInputAndOutputTest([input], output, prepareRpcAction);
    }

    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    private async Task ExpectedServerInputAndOutputTest(string[] input, string output, Action<SimpleJsonRpc>? prepareRpcAction = null)
    {
        byte[] outputBytes = Encoding.UTF8.GetBytes(output);
        byte[] readBuffer = new byte[Math.Max(4000, outputBytes.Length)];
        ArrayBufferWriter<byte> buffer = new();
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        SimpleJsonRpc rpc = new(serverStream);
        prepareRpcAction?.Invoke(rpc);
        rpc.StartListening();
        foreach (var inputBytes in input.Select(x => Encoding.UTF8.GetBytes(x)))
        {
            await clientStream.WriteAsync(inputBytes.ToArray());
            await clientStream.FlushAsync();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            while (!cts.IsCancellationRequested)
            {
                int read = await clientStream.ReadAsync(readBuffer, cts.Token);
                Console.WriteLine($"Read: {read}");
                buffer.Write(new ReadOnlySpan<byte>(readBuffer, 0, read));
                if (outputBytes.Length != 0 && buffer.WrittenCount == outputBytes.Length) break;
            }
        }
        catch (OperationCanceledException)
        {
        }

        string recievedLine = Encoding.UTF8.GetString(buffer.WrittenSpan);
        log.WriteLine($"Expected: '{output.ReplaceLineEndings("\\n")}'");
        log.WriteLine($"Got:      '{recievedLine.ReplaceLineEndings("\\n")}'");
        Assert.Equal(output, recievedLine);
        await serverStream.DisposeAsync();
        Assert.Equal(0, await clientStream.ReadAsync(readBuffer));
    }

    [Theory]
    [InlineData("5\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32700,\"message\":\"Value is not an object or array\"},\"id\":null}\n")]
    [InlineData("bad\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32700,\"message\":\"Invalid json\"},\"id\":null}\n")]
    [InlineData("{\"jsonrpc\":\"1.0\"}\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32600,\"message\":\"Invalid jsonrpc version\"},\"id\":null}\n")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":{}}\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32600,\"message\":\"Invalid id value\"},\"id\":null}\n")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":false}\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32600,\"message\":\"Invalid id value\"},\"id\":null}\n")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":15.9875}\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32601,\"message\":\"Method not found\"},\"id\":15.9875}\n")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"m\",\"id\":15.9875}\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32601,\"message\":\"Method not found\"},\"id\":15.9875}\n")]
    public async Task SimpleJsonRpcServer_InvalidRequest_ReturnsError(string input, string output)
    {
        await ExpectedServerInputAndOutputTest(input, output);
    }

    public static TheoryData<string, string> SimpleJsonRpcServerSumMethodData => new()
    {
        { "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":{\"A\":5,\"B\":6},\"id\":1}\n", "{\"jsonrpc\":\"2.0\",\"result\":11,\"id\":1}\n" },
        { "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":{\"A\":5,\"C\":6},\"id\":1}\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32602,\"message\":\"Failed to parse params\"},\"id\":1}\n" },
        { "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"id\":1}\n", "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32602,\"message\":\"Invalid params\"},\"id\":1}\n" },
        { "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":{\"A\":5,\"B\":6}}\n", "" },
        {
            "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":{\"A\":5,\"B\":6},\"id\":1}\n" +
            "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":{\"B\":5,\"A\":12},\"id\":2}\n" +
            "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":{\"A\":\"bad\",\"B\":12},\"id\":3}\n" +
            "{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":{\"A\":100,\"B\":-1000},\"id\":4}\n",

            "{\"jsonrpc\":\"2.0\",\"result\":11,\"id\":1}\n" +
            "{\"jsonrpc\":\"2.0\",\"result\":17,\"id\":2}\n" +
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32602,\"message\":\"Failed to parse params\"},\"id\":3}\n" +
            "{\"jsonrpc\":\"2.0\",\"result\":-900,\"id\":4}\n"
        }
    };

    [Theory]
    [MemberData(nameof(SimpleJsonRpcServerSumMethodData))]
    public async Task SimpleJsonRpcServer_SumMethod_ReturnsResultOrParamsError(string input, string output)
    {
        await ExpectedServerInputAndOutputTest(input, output, rpc => rpc.RegisterMethod("sum", new Func<TwoNumbers, int>(param => param.A + param.B)));
    }

    [Theory]
    [MemberData(nameof(SimpleJsonRpcServerSumMethodData))]
    public async Task SimpleJsonRpcServer_TaskSumMethod_ReturnsResultOrParamsError(string input, string output)
    {
        await ExpectedServerInputAndOutputTest(input, output, rpc =>
        {
            rpc.RegisterMethod("sum", new Func<TwoNumbers, Task<int>>(async param =>
            {
                await Task.CompletedTask;
                return param.A + param.B;
            }));
        });
    }

    [Theory]
    [MemberData(nameof(SimpleJsonRpcServerSumMethodData))]
    public async Task SimpleJsonRpcServer_ValueTaskSumMethod_ReturnsResultOrParamsError(string input, string output)
    {
        await ExpectedServerInputAndOutputTest(input, output, rpc =>
        {
            rpc.RegisterMethod("sum", new Func<TwoNumbers, ValueTask<int>>(async param =>
            {
                await Task.CompletedTask;
                return param.A + param.B;
            }));
        });
    }

    private async Task VoidResultMethodTest(Delegate @delegate)
    {
        await ExpectedServerInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"sum\", \"params\":{\"A\":5,\"B\":6},\"id\":1}\n",
            "{\"jsonrpc\":\"2.0\",\"result\":null,\"id\":1}\n",
            rpc => rpc.RegisterMethod("sum", @delegate)
        );
    }

    [Fact]
    public async Task SimpleJsonRpcServer_VoidResultMethod_ReturnsNullResult()
    {
        await VoidResultMethodTest(new Action<TwoNumbers>(param => { param.A += param.B; }));
    }

    [Fact]
    public async Task SimpleJsonRpcServer_TaskVoidResultMethod_ReturnsNullResult()
    {
        await VoidResultMethodTest(new Func<TwoNumbers, Task>(async param =>
        {
            await Task.CompletedTask;
            param.A += param.B;
        }));
    }


    [Fact]
    public async Task SimpleJsonRpcServer_ValueTaskVoidResultMethod_ReturnsNullResult()
    {
        await VoidResultMethodTest(new Func<TwoNumbers, ValueTask>(async param =>
        {
            await Task.CompletedTask;
            param.A += param.B;
        }));
    }

    [Fact]
    public async Task SimpleJsonRpcServer_ParameterlessMethod_Succeeds()
    {
        await ExpectedServerInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"twoCryptographicallySecureNumbers\",\"id\":1}\n",
            "{\"jsonrpc\":\"2.0\",\"result\":{\"A\":4,\"B\":4},\"id\":1}\n",
            rpc => rpc.RegisterMethod("twoCryptographicallySecureNumbers", new Func<TwoNumbers>(() => new() { A = 4, B = 4 }))
        );
    }

    [Fact]
    public async Task SimpleJsonRpcServer_MethodThrowsUserDefinedError_ForwardsSuccessfully()
    {
        await ExpectedServerInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"wellWrittenMethod\",\"id\":1}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":2,\"message\":\"Error 2\"},\"id\":1}\n",
            rpc => rpc.RegisterMethod("wellWrittenMethod", new Func<TwoNumbers>(() => throw new JsonRpcRequestException(new(2, "Error 2"))))
        );
    }

    [Fact]
    public async Task SimpleJsonRpcServer_MethodThrowsUserDefinedErrorWithExtraData_ForwardsSuccessfully()
    {
        await ExpectedServerInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"wellWrittenMethod\",\"id\":1}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"data\":{\"A\":2,\"B\":2},\"code\":2,\"message\":\"Error 2\"},\"id\":1}\n",
            rpc => rpc.RegisterMethod("wellWrittenMethod", new Func<TwoNumbers>(() => throw new JsonRpcRequestException(new JsonRpcError<TwoNumbers>(2, "Error 2", new() { A = 2, B = 2 }))))
        );
    }

    [Fact]
    public async Task SimpleJsonRpcServer_MethodThrowsUserDefinedErrorThatFailsToSerialize_RespondsWithGenericError()
    {
        await ExpectedServerInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"wellWrittenMethod\",\"id\":1}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32603,\"message\":\"Failed to serialize application error\"},\"id\":1}\n",
            rpc => rpc.RegisterMethod("wellWrittenMethod", new Func<TwoNumbers>(() => throw new JsonRpcRequestException(new JsonRpcError<Type>(2, "Error 2", typeof(JsonRpcError)))))
        );
    }

    [Fact]
    public async Task SimpleJsonRpcServer_MethodThrowsUndefinedError_RespondsWithGenericError()
    {
        await ExpectedServerInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"wellWrittenMethod\",\"id\":1}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32603,\"message\":\"Failed to execute method\"},\"id\":1}\n",
            rpc => rpc.RegisterMethod("wellWrittenMethod", new Func<TwoNumbers>(() => throw new Exception()))
        );
    }

    [Fact]
    public async Task SimpleJsonRpcServer_MethodResultNotSerializable_RespondsWithError()
    {
        await ExpectedServerInputAndOutputTest(
            "{\"jsonrpc\":\"2.0\",\"method\":\"badlyWrittenMethod\",\"id\":1}\n",
            "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32603,\"message\":\"Failed to serialize response\"},\"id\":1}\n",
            rpc => rpc.RegisterMethod("badlyWrittenMethod", new Func<Type>(() => typeof(TwoNumbers)))
        );
    }

    [Fact]
    public async Task SimpleJsonRpcServer_FracturedInput_ParsedCorrectuly()
    {
        await ExpectedServerInputAndOutputTest(
            [
                "{\"jsonrpc\":\"2.0\",\"method\":\"method\",\"id\":1}\n{\"jsonrpc\":\"2.",
                "0\",\"method\":\"method\",\"id\":1}\n{\"jsonrpc\":\"2.0\",",
                "\"method\":\"method\",\"id\":1}",
                "\n"
            ],
            "{\"jsonrpc\":\"2.0\",\"result\":null,\"id\":1}\n" +
            "{\"jsonrpc\":\"2.0\",\"result\":null,\"id\":1}\n" +
            "{\"jsonrpc\":\"2.0\",\"result\":null,\"id\":1}\n",
            rpc => rpc.RegisterMethod("method", new Action(() => { }))
        );
    }
}