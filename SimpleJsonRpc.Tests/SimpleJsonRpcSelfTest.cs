using System.Buffers;
using System.Text;
using Nerdbank.Streams;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Exceptions;
using SimpleJsonRpc.Tests.Data;
using Xunit.Abstractions;

namespace SimpleJsonRpc.Tests;

public class SimpleJsonRpcSelfTest
{
    private int Sum(TwoNumbers numbers) => numbers.A + numbers.B;

    [Fact]
    public async Task SimpleJsonRpcSelfTest_TwoInstances_Succeeds()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        SimpleJsonRpc serverRpc = new(serverStream);
        serverRpc.RegisterMethod("sum", Sum);
        serverRpc.StartListening();
        SimpleJsonRpc clientRpc = new(clientStream);
        clientRpc.StartListening();
        Assert.Equal(2, await clientRpc.Request<int>("sum", new TwoNumbers(1, 1)));
        Assert.Equal(4, await clientRpc.Request<int>("sum", new TwoNumbers(2, 2)));
        Assert.Equal(6, await clientRpc.Request<int>("sum", new TwoNumbers(3, 3)));
        Assert.Equal(8, await clientRpc.Request<int>("sum", new TwoNumbers(4, 4)));
    }

    [Fact]
    public async Task SimpleJsonRpcSelfTest_DualModeRequest_Succeeds()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        SimpleJsonRpc rpc = new(serverStream, clientStream);
        rpc.RegisterMethod("sum", Sum);
        rpc.StartListening();
        Assert.Equal(2, await rpc.Request<int>("sum", new TwoNumbers(1, 1)));
        Assert.Equal(4, await rpc.Request<int>("sum", new TwoNumbers(2, 2)));
        Assert.Equal(6, await rpc.Request<int>("sum", new TwoNumbers(3, 3)));
        Assert.Equal(8, await rpc.Request<int>("sum", new TwoNumbers(4, 4)));
    }
}