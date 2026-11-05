using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace SimpleJsonRpc.Delegates;

internal static class DelegateInvoker
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> ResultProperties = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> AsTaskMethods = new();

    public static async Task<object?> InvokeAsync(Delegate @delegate, params object?[]? args)
    {
        object? result;
        try
        {
            result = @delegate.DynamicInvoke(args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        // Handle Task and Task<>
        if (result is Task task) return await AwaitTaskAsync(task).ConfigureAwait(false);

        // Handle ValueTask
        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return null;
        }

        // Handle ValueTask<>
        if (result?.GetType().IsGenericType == true && result.GetType().GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            MethodInfo asTaskMethod = AsTaskMethods.GetOrAdd(result.GetType(), t => t.GetMethod(nameof(ValueTask<int>.AsTask), Type.EmptyTypes)!);
            Task resultAsTask = (Task)asTaskMethod.Invoke(result, null)!;
            return await AwaitTaskAsync(resultAsTask).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task<object?> AwaitTaskAsync(Task task)
    {
        await task.ConfigureAwait(false);
        var property = ResultProperties.GetOrAdd(task.GetType(), static t =>
        {
            var prop = t.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);

            // Task without a return type are often just Task<VoidTaskResult>
            return prop?.PropertyType.Name == "VoidTaskResult" ? null : prop;
        });

        return property?.GetValue(task);
    }
}