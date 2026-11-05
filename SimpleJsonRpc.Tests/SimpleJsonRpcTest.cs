using System.Buffers;
using System.Text;
using Nerdbank.Streams;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Exceptions;
using Xunit.Abstractions;

namespace SimpleJsonRpc.Tests;

public class SimpleJsonRpcTest
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
        await rpc.DisposeAsync();
        Assert.True(rpc.IsDisposed);
        Assert.True(rpc.Completion.IsCompleted);
        JsonRpcShutdownReason shutdownReason = await rpc.Completion;
        Assert.Equal(new(JsonRpcShutdownType.Disposed, null), shutdownReason);
    }

    [Fact]
    public async Task SimpleJsonRpc_DisposeWriteStreamAndAttemptRequest_TaskFinishesWithException()
    {
        Stream readStream = new MemoryStream();
        Stream writeStream = new MemoryStream();
        SimpleJsonRpc rpc = new(readStream, writeStream);
        rpc.StartListening();
        await writeStream.DisposeAsync();
        await rpc.Notify("test");
        Assert.True(rpc.IsDisposed);
        Assert.True(rpc.Completion.IsCompleted);
        JsonRpcShutdownReason shutdownReason = await rpc.Completion;
        Assert.Equal(JsonRpcShutdownType.WritingError, shutdownReason.Reason);
        Assert.Equal(typeof(ObjectDisposedException), shutdownReason.Exception?.GetType());
    }

    [Fact]
    public async Task SimpleJsonRpc_DisposeReadStream_TaskFinishesWithException()
    {
        Stream readStream = new MemoryStream();
        Stream writeStream = new MemoryStream();
        SimpleJsonRpc rpc = new(readStream, writeStream);
        rpc.StartListening();
        await readStream.DisposeAsync();
        JsonRpcShutdownReason shutdownReason = await rpc.Completion.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);
        Assert.Equal(JsonRpcShutdownType.ListeningError, shutdownReason.Reason);
        Assert.Equal(typeof(ObjectDisposedException), shutdownReason.Exception?.GetType());
    }
}