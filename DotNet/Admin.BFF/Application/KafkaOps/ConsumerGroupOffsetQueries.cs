namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public static class ConsumerGroupOffsetQueries
{
    public const int MaxParallel = 4;

    public static async Task<List<T>> ListPerGroupAsync<T>(
        IReadOnlyList<string> groupIds,
        Func<string, CancellationToken, Task<IReadOnlyList<T>>> listOne,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
            return [];

        var gate = new SemaphoreSlim(MaxParallel);
        var batches = new IReadOnlyList<T>[groupIds.Count];
        var reads = new Task[groupIds.Count];
        for (var index = 0; index < groupIds.Count; index++)
        {
            var captured = index;
            reads[index] = ReadAsync(captured);
        }

        await Task.WhenAll(reads);
        gate.Dispose();
        var rows = new List<T>(groupIds.Count);
        foreach (var batch in batches)
        {
            if (batch is not null)
                rows.AddRange(batch);
        }

        return rows;

        async Task ReadAsync(int index)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                batches[index] = await listOne(groupIds[index], cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
