using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

[Collection(IntegrationTestCollection.Name)]
public class MongoSnapshotStoreLogOrderingTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public MongoSnapshotStoreLogOrderingTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Concurrent_appends_return_logs_in_source_order_even_if_completion_order_differs()
    {
        var store = _fixture.CreateStore();
        var runId = Guid.NewGuid();

        var firstBatch = Enumerable.Range(0, 3_000).Select(i => $"source-a-{i:D4}").ToList();
        var secondBatch = new List<string> { "source-b-0000" };

        var appendFirst = Task.Run(() => store.AppendLogsAsync(runId, firstBatch, CancellationToken.None));
        await WaitForReservedSequenceCountAsync(runId, firstBatch.Count, CancellationToken.None);

        var appendSecond = Task.Run(() => store.AppendLogsAsync(runId, secondBatch, CancellationToken.None));

        var completedFirst = await Task.WhenAny(appendFirst, appendSecond);
        completedFirst.Should().Be(appendSecond);

        await Task.WhenAll(appendFirst, appendSecond);

        var logs = await store.GetLogsAsync(runId, CancellationToken.None);
        logs.Should().ContainInOrder(firstBatch.Concat(secondBatch));
    }

    private async Task WaitForReservedSequenceCountAsync(Guid runId, int expectedNextSequence, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", runId.ToString());

        for (var i = 0; i < 200; i++)
        {
            var sequenceDocument = await _fixture.RawLogSequenceCollection.Find(filter).FirstOrDefaultAsync(ct);
            if (sequenceDocument != null
                && sequenceDocument.TryGetValue("NextSequence", out var value)
                && value.IsNumeric
                && value.ToInt64() >= expectedNextSequence)
            {
                return;
            }

            await Task.Delay(10, ct);
        }

        throw new TimeoutException("Timed out waiting for initial log sequence reservation.");
    }
}
