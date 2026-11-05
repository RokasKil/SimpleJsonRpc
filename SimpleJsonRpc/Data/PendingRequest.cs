namespace SimpleJsonRpc.Data;

internal abstract class PendingRequest(Type responseType)
{
    public Type ResponseType { get; } = responseType;
    public abstract void SetResult(object? value);
    public abstract void SetException(Exception exception);
    public abstract void SetCancelled(CancellationToken cancellationToken);
}

internal class PendingRequest<TResult>() : PendingRequest(typeof(TResult))
{
    public TaskCompletionSource<TResult> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void SetResult(object? value)
    {
        CompletionSource.TrySetResult((TResult)value!);
    }

    public override void SetException(Exception exception)
    {
        CompletionSource.TrySetException(exception);
    }

    public override void SetCancelled(CancellationToken cancellationToken)
    {
        CompletionSource.TrySetCanceled(cancellationToken);
    }
}