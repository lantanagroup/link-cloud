using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class ConsumerGroupOffsetQueryTests
{
    [Fact]
    public async Task ListPerGroup_CallsEachGroupOnce_AndBoundsParallelism()
    {
        var calls = new List<string>();
        var ids = new[] { "g1", "g2", "g3" };
        var rows = await ConsumerGroupOffsetQueries.ListPerGroupAsync(
            ids,
            (groupId, _) =>
            {
                lock (calls)
                    calls.Add(groupId);
                IReadOnlyList<string> batch = [groupId];
                return Task.FromResult(batch);
            },
            CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), calls.OrderBy(id => id, StringComparer.Ordinal));

        var observed = 0;
        var peak = 0;
        var many = Enumerable.Range(0, 8).Select(index => "g" + index).ToList();
        await ConsumerGroupOffsetQueries.ListPerGroupAsync(
            many,
            async (_, token) =>
            {
                var now = Interlocked.Increment(ref observed);
                int snapshot;
                while ((snapshot = Volatile.Read(ref peak)) < now
                    && Interlocked.CompareExchange(ref peak, now, snapshot) != snapshot)
                {
                }

                await Task.Delay(40, token);
                Interlocked.Decrement(ref observed);
                return (IReadOnlyList<int>)[1];
            },
            CancellationToken.None);

        Assert.Equal(1, ConsumerGroupOffsetQueries.MaxParallel);
        Assert.InRange(peak, 1, ConsumerGroupOffsetQueries.MaxParallel);
    }

    [Fact]
    public void ClusterHealth_SeparatesAnOnlineIsrShrinkFromAnOfflinePartition()
    {
        var shrunk = ClusterHealthCounts.ForPartition(1, [1, 2, 3], [1, 2]);
        Assert.Equal(1, shrunk.UnderReplicated);
        Assert.Equal(1, shrunk.IsrShrunk);
        Assert.Equal(0, shrunk.Offline);

        var healthy = ClusterHealthCounts.ForPartition(1, [1, 2], [1, 2]);
        Assert.Equal(0, healthy.UnderReplicated);
        Assert.Equal(0, healthy.IsrShrunk);
        Assert.Equal(0, healthy.Offline);

        var offline = ClusterHealthCounts.ForPartition(-1, [1, 2], [1]);
        Assert.Equal(1, offline.UnderReplicated);
        Assert.Equal(0, offline.IsrShrunk);
        Assert.Equal(1, offline.Offline);
    }

    [Fact]
    public void Gateway_ListsOffsetsOneGroupAtATime()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "DotNet", "Admin.BFF", "Application", "KafkaOps", "KafkaBrokerGateway.cs"));
        Assert.Contains("ListPerGroupAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ListConsumerGroupOffsetsAsync(ids", text, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "topics.txt")) && Directory.Exists(Path.Combine(dir.FullName, "DotNet")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("The repository root was not found from the test output directory.");
    }
}
