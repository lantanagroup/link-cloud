using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using MongoDB.Driver;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

[Collection(IntegrationTestCollection.Name)]
public class MongoApiHealthRunStoreTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;
    private readonly IMongoDatabase _database;
    private readonly MongoApiHealthRunStore _store;

    public MongoApiHealthRunStoreTests(
        AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _database = fixture.Database;
        _store = new MongoApiHealthRunStore(_database);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveRunResultsAsync_PersistsEndpointResultSeparately()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var result = MakeResult(
            runId,
            "Tenant",
            "Tenant.Info",
            startedAt);

        // Act
        await _store.SaveRunResultsAsync(
            [result],
            "Single",
            startedAt);

        // Assert
        var runCollection =
            _database.GetCollection<ApiHealthRunDocument>("api_health_runs");

        var resultCollection =
            _database.GetCollection<ApiHealthRunResultDocument>(
                "api_health_run_results");

        var runDoc = await runCollection
            .Find(d =>
                d.RunId == runId &&
                d.ServiceName == "Tenant")
            .SingleAsync();

        var resultDoc = await resultCollection
            .Find(d =>
                d.RunId == runId &&
                d.ServiceName == "Tenant" &&
                d.EndpointKey == "Tenant.Info")
            .SingleAsync();

        runDoc.EndpointResults.Should().BeEmpty();

        resultDoc.Result.RunId.Should().Be(runId);
        resultDoc.Result.ServiceName.Should().Be("Tenant");
        resultDoc.Result.EndpointKey.Should().Be("Tenant.Info");
    }

    [Fact]
    public async Task SaveRunResultsAsync_UpsertsExistingEndpointResult()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var first = MakeResult(
            runId,
            "Tenant",
            "Tenant.Info",
            startedAt,
            responseBody: "first response");

        var updated = MakeResult(
            runId,
            "Tenant",
            "Tenant.Info",
            startedAt.AddSeconds(1),
            responseBody: "updated response");

        // Act
        await _store.SaveRunResultsAsync(
            [first],
            "Single",
            startedAt);

        await _store.SaveRunResultsAsync(
            [updated],
            "Single",
            startedAt);

        // Assert
        var collection =
            _database.GetCollection<ApiHealthRunResultDocument>(
                "api_health_run_results");

        var docs = await collection
            .Find(d =>
                d.RunId == runId &&
                d.ServiceName == "Tenant" &&
                d.EndpointKey == "Tenant.Info")
            .ToListAsync();

        docs.Should().ContainSingle();
        docs[0].Result.ResponseBody.Should().Be("updated response");
    }

    [Fact]
    public async Task SaveRunResultsAsync_PreservesFullRequestAndResponseBodies()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var requestBody = new string('r', 1000);
        var responseBody = new string('s', 1000);

        var result = MakeResult(
            runId,
            "Tenant",
            "Tenant.Create",
            startedAt,
            requestBody,
            responseBody);

        // Act
        await _store.SaveRunResultsAsync(
            [result],
            "Single",
            startedAt);

        var saved = await _store.GetLatestResultsForRunAsync(
            runId,
            ["Tenant.Create"]);

        // Assert
        saved.Should().ContainKey("Tenant.Create");

        saved["Tenant.Create"].RequestBody
            .Should().Be(requestBody);

        saved["Tenant.Create"].ResponseBody
            .Should().Be(responseBody);

        saved["Tenant.Create"].RequestBody
            .Should().HaveLength(1000);

        saved["Tenant.Create"].ResponseBody
            .Should().HaveLength(1000);
    }

    [Fact]
    public async Task GetLatestResultsByServiceAsync_ReturnsLatestServiceRun()
    {
        // Arrange
        var olderRunId = Guid.NewGuid();
        var newerRunId = Guid.NewGuid();

        var olderStartedAt =
            DateTimeOffset.UtcNow.AddMinutes(-10);

        var newerStartedAt =
            DateTimeOffset.UtcNow;

        var older = MakeResult(
            olderRunId,
            "Tenant",
            "Tenant.Info",
            olderStartedAt,
            responseBody: "older");

        var newer = MakeResult(
            newerRunId,
            "Tenant",
            "Tenant.Info",
            newerStartedAt,
            responseBody: "newer");

        await _store.SaveRunResultsAsync(
            [older],
            "Single",
            olderStartedAt);

        await _store.SaveRunResultsAsync(
            [newer],
            "Single",
            newerStartedAt);

        // Act
        var results =
            await _store.GetLatestResultsByServiceAsync(
                ["Tenant.Info"]);

        // Assert
        results.Should().ContainKey("Tenant.Info");
        results["Tenant.Info"].RunId.Should().Be(newerRunId);
        results["Tenant.Info"].ResponseBody.Should().Be("newer");
    }

    [Fact]
    public async Task GetLatestResultsForRunAsync_ReturnsSeparatelyPersistedResults()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var result = MakeResult(
            runId,
            "Tenant",
            "Tenant.Health",
            startedAt,
            responseBody: "new persistence");

        await _store.SaveRunResultsAsync(
            [result],
            "Single",
            startedAt);

        // Act
        var results =
            await _store.GetLatestResultsForRunAsync(
                runId,
                ["Tenant.Health"]);

        // Assert
        results.Should().ContainKey("Tenant.Health");

        results["Tenant.Health"].RunId
            .Should().Be(runId);

        results["Tenant.Health"].ResponseBody
            .Should().Be("new persistence");
    }

    [Fact]
    public async Task GetLatestResultsForRunAsync_ReturnsLegacyEmbeddedResults()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var legacyResult = MakeResult(
            runId,
            "Tenant",
            "Tenant.Legacy",
            startedAt,
            responseBody: "legacy response");

        var legacyDoc = new ApiHealthRunDocument
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            ServiceName = "Tenant",
            RunMode = "Single",
            StartedAt = startedAt,
            EndpointResults = [legacyResult]
        };

        var collection =
            _database.GetCollection<ApiHealthRunDocument>(
                "api_health_runs");

        await collection.InsertOneAsync(legacyDoc);

        // Act
        var results =
            await _store.GetLatestResultsForRunAsync(
                runId,
                ["Tenant.Legacy"]);

        // Assert
        results.Should().ContainKey("Tenant.Legacy");

        results["Tenant.Legacy"].RunId
            .Should().Be(runId);

        results["Tenant.Legacy"].ResponseBody
            .Should().Be("legacy response");
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsResultsInDescendingOrderWithPaging()
    {
        // Arrange
        var endpointKey = $"Tenant.Info.History.{Guid.NewGuid():N}";

        var oldestRunId = Guid.NewGuid();
        var middleRunId = Guid.NewGuid();
        var newestRunId = Guid.NewGuid();

        var now = DateTimeOffset.UtcNow;

        var oldest = MakeResult(
            oldestRunId,
            "Tenant",
            endpointKey,
            now.AddMinutes(-20),
            responseBody: "oldest");

        var middle = MakeResult(
            middleRunId,
            "Tenant",
            endpointKey,
            now.AddMinutes(-10),
            responseBody: "middle");

        var newest = MakeResult(
            newestRunId,
            "Tenant",
            endpointKey,
            now,
            responseBody: "newest");

        await _store.SaveRunResultsAsync(
            [oldest],
            "Single",
            oldest.ExecutedAt);

        await _store.SaveRunResultsAsync(
            [middle],
            "Single",
            middle.ExecutedAt);

        await _store.SaveRunResultsAsync(
            [newest],
            "Single",
            newest.ExecutedAt);

        // Act
        var firstPage =
            await _store.GetHistoryAsync(
                endpointKey,
                pageNumber: 1,
                pageSize: 2);

        var secondPage =
            await _store.GetHistoryAsync(
                endpointKey,
                pageNumber: 2,
                pageSize: 2);

        // Assert
        firstPage.TotalCount.Should().Be(3);
        firstPage.Runs.Should().HaveCount(2);

        firstPage.Runs[0].RunId.Should().Be(newestRunId);
        firstPage.Runs[1].RunId.Should().Be(middleRunId);

        secondPage.TotalCount.Should().Be(3);
        secondPage.Runs.Should().ContainSingle();

        secondPage.Runs[0].RunId.Should().Be(oldestRunId);
    }

    private static ApiTestRunResult MakeResult(
        Guid runId,
        string serviceName,
        string endpointKey,
        DateTimeOffset executedAt,
        string requestBody = "{}",
        string responseBody = "{}")
    {
        return new ApiTestRunResult
        {
            RunId = runId,
            ServiceName = serviceName,
            EndpointKey = endpointKey,
            ExecutedAt = executedAt,
            RequestBody = requestBody,
            ResponseBody = responseBody
        };
    }
}