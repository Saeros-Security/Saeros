namespace Collector.Databases.Implementation.Extensions;

internal static class ManualResetEventSlimExtensions
{
    public static Task WaitAsync(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken = default)
        => WaitAsync(manualResetEvent.WaitHandle, cancellationToken);

    private static Task WaitAsync(this WaitHandle waitHandle, CancellationToken cancellationToken = default)
    {
        CancellationTokenRegistration cancellationRegistration = default;

        var tcs = new TaskCompletionSource();
        var handle = ThreadPool.RegisterWaitForSingleObject(
            waitObject: waitHandle,
            callBack: (_, _) =>
            {
                cancellationRegistration.Unregister();
                tcs.TrySetResult();
            },
            state: null,
            timeout: Timeout.InfiniteTimeSpan,
            executeOnlyOnce: true);

        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(() =>
            {
                handle.Unregister(waitHandle);
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        return tcs.Task;
    }
}