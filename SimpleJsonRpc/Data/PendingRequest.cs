namespace SimpleJsonRpc.Data;

internal abstract class PendingRequest(Type responseType)
{
    public Type ResponseType { get; } = responseType;
    public abstract void SetResult(object? value);
    public abstract void SetException(Exception exception);
    public abstract void SetCancelled();
}

internal class PendingRequest<T>() : PendingRequest(typeof(T))
{
    public TaskCompletionSource<T> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void SetResult(object? value)
    {
        CompletionSource.TrySetResult((T)value!);
    }

    public override void SetException(Exception exception)
    {
        CompletionSource.TrySetException(exception);
    }

    public override void SetCancelled()
    {
        CompletionSource.TrySetCanceled();
    }
}