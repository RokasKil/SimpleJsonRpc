using System.Buffers;
using System.Text;
using Nerdbank.Streams;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Exceptions;
using Xunit.Abstractions;

namespace SimpleJsonRpc.Tests;

public class SimpleJsonRpcTest(ITestOutputHelper log)
{
    [Fact]
    public void RegisterMethod_NoParameters_Succeeds()
    {
        SimpleJsonRpc rpc = new();
        rpc.RegisterMethod("name", new Action(() => { }));
    }

    [Fact]
    public void RegisterMethod_OneParameter_Succeeds()
    {
        SimpleJsonRpc rpc = new();
        rpc.RegisterMethod("name", new Action<int>((a) => { }));
    }

    [Fact]
    public void RegisterMethod_TooManyParameters_ThrowsException()
    {
        SimpleJsonRpc rpc = new();
        Assert.Throws<InvalidOperationException>(() => rpc.RegisterMethod("name", new Action<int, int>((a, b) => { })));
    }


    [Fact]
    public void SimpleJsonRpc_StartListening_Succeeds()
    {
        SimpleJsonRpc rpc = new();
        rpc.StartListening();
        Assert.True(rpc.IsStarted);
        Assert.False(rpc.IsDisposed);
    }

    [Fact]
    public async Task SimpleJsonRpc_Shutdown_TaskFinishes()
    {
        SimpleJsonRpc rpc = new();
        rpc.StartListening();
        Assert.True(rpc.IsStarted);
        await rpc.Shutdown();
        Assert.True(rpc.IsDisposed);
        Assert.True(rpc.Completion.IsCompleted);
    }

    #region Test class definitions

    private struct TwoNumbers
    {
        public required int A { get; set; }
        public required int B { get; set; }
    }

    #endregion
}