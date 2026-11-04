using SimpleJsonRpc.Delegates;

namespace SimpleJsonRpc.Tests;

public class DelegateInvokerTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("test")]
    [InlineData(153)]
    public async Task InvokeAsync_Identity_ReturnsProvidedValue(object? value)
    {
        Assert.Equal(value, await DelegateInvoker.InvokeAsync(new Func<object?, object?>(param => param), value));
    }


    public static TheoryData<Task, object?> InvokeAsyncTaskReturnsTasksResultTaskData => new()
    {
        { Task.CompletedTask, null },
        { Task.FromResult(42), 42 },
        { Task.CompletedTask.ContinueWith(_ => 100), 100 },
    };

    [Theory]
    [MemberData(nameof(InvokeAsyncTaskReturnsTasksResultTaskData))]
    public async Task InvokeAsync_TaskValue_ReturnsTasksResult(Task value, object? result)
    {
        Assert.Equal(result, await DelegateInvoker.InvokeAsync(new Func<object?, object?>(param => param), value));
    }

    [Fact]
    public async Task InvokeAsync_ValueTask_ReturnsNull()
    {
        Assert.Null(await DelegateInvoker.InvokeAsync(new Func<ValueTask>(() => ValueTask.CompletedTask)));
    }


    [Fact]
    public async Task InvokeAsync_GenericValueTask_ReturnsValue()
    {
        Assert.Equal(5, await DelegateInvoker.InvokeAsync(new Func<ValueTask<int>>(() => ValueTask.FromResult(5))));
    }

    [Fact]
    public async Task InvokeAsync_ErroredValueTaskExcepted_ForwardsException()
    {
        await Assert.ThrowsAsync<TestException>(async () => await DelegateInvoker.InvokeAsync(new Func<ValueTask>(() => ValueTask.FromException(new TestException()))));
    }

    [Fact]
    public async Task InvokeAsync_CancelledValueTask_ForwardsException()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await DelegateInvoker.InvokeAsync(new Func<ValueTask>(() => ValueTask.FromCanceled(cts.Token))));
    }

    #region Test class definitions

    private class TestException : Exception
    {
    }

    #endregion
}