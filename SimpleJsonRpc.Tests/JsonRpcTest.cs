using System.Buffers;
using System.Text;
using Nerdbank.Streams;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Exceptions;
using Xunit.Abstractions;

namespace SimpleJsonRpc.Tests;

public class JsonRpcTest
{
    [Fact]
    public async Task RegisterMethod_NoParameters_Succeeds()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        await using JsonRpc rpc = new(serverStream);
        rpc.RegisterMethod("name", new Action(() => { }));
    }

    [Fact]
    public async Task RegisterMethod_OneParameter_Succeeds()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        await using JsonRpc rpc = new(serverStream, clientStream);
        rpc.RegisterMethod("name", new Action<int>((a) => { }));
    }

    [Fact]
    public async Task RegisterMethod_TooManyParameters_ThrowsException()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        await using JsonRpc rpc = new(serverStream, clientStream);
        Assert.Throws<InvalidOperationException>(() => rpc.RegisterMethod("name", new Action<int, int>((a, b) => { })));
    }


    [Fact]
    public async Task SimpleJsonRpc_StartListening_Succeeds()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        await using JsonRpc rpc = new(serverStream, clientStream);
        rpc.StartListening();
        Assert.True(rpc.IsStarted);
        Assert.False(rpc.IsDisposed);
    }

    [Fact]
    public async Task SimpleJsonRpc_Shutdown_TaskFinishes()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        await using JsonRpc rpc = new(serverStream, clientStream);
        rpc.StartListening();
        Assert.True(rpc.IsStarted);
        await rpc.DisposeAsync();
        Assert.True(rpc.IsDisposed);
        Assert.True(rpc.Completion.IsCompleted);
        JsonRpcShutdownReason shutdownReason = await rpc.Completion;
        Assert.Equal(new(JsonRpcShutdownType.Disposed, null), shutdownReason);
    }

    [Fact]
    public async Task SimpleJsonRpc_DisposeWriteStreamAndAttemptRequest_TaskFinishesWithException()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        var (clientStream2, serverStream2) = FullDuplexStream.CreatePair();
        await using JsonRpc rpc = new(serverStream, clientStream2);
        rpc.StartListening();
        await clientStream2.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await rpc.Notify("test"));
        Assert.True(rpc.IsDisposed);
        Assert.True(rpc.Completion.IsCompleted);
        JsonRpcShutdownReason shutdownReason = await rpc.Completion;
        Assert.Equal(JsonRpcShutdownType.WritingError, shutdownReason.Reason);
        Assert.Equal(typeof(InvalidOperationException), shutdownReason.Exception?.GetType());
    }

    [Fact]
    public async Task SimpleJsonRpc_DisposeReadStream_TaskFinishesWithException()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        await using JsonRpc rpc = new(serverStream, clientStream);
        rpc.StartListening();
        await serverStream.DisposeAsync();
        JsonRpcShutdownReason shutdownReason = await rpc.Completion.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);
        Assert.Equal(JsonRpcShutdownType.ListeningError, shutdownReason.Reason);
        Assert.Equal(typeof(InvalidOperationException), shutdownReason.Exception?.GetType());
    }
}