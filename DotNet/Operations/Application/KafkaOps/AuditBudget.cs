namespace LantanaGroup.Link.Operations.Application.KafkaOps;

/// <summary>
/// Runs an audit write off the request path with a hard budget. The Kafka producer can block the calling thread
/// for up to a minute while it waits for metadata on a missing audit topic, and it does not always honour the
/// cancellation token while it does, so the write is started on the thread pool and the caller stops waiting
/// when the budget runs out. A late write still completes or fails on its own and its fault is observed.
/// </summary>
public static class AuditBudget
{
    public static async Task WaitAsync(Func<CancellationToken, Task> write, TimeSpan budget, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(budget);
        var token = limit.Token;
        var work = Task.Run(() => write(token), CancellationToken.None);
        var finished = await Task.WhenAny(work, Task.Delay(budget, cancellationToken)).ConfigureAwait(false);
        if (finished == work)
        {
            await work.ConfigureAwait(false);
            return;
        }

        _ = work.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException("The audit write did not finish within " + budget.TotalSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s.");
    }
}
