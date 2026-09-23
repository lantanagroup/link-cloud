using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ApiHealthExecutionRunManagerTests
{
    private const string TestServiceName = "TestService";
    private const string TestEndpointName = "Test Endpoint";
    private const string TestEndpointKey = "TestService::TestEndpoint";

    [Fact]
    public async Task PassedServiceInfo_StampsDeploymentMetadataOnResults()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: true,
                responseBody: """
                {
                  "serviceName": "TestService",
                  "version": "0.7.1+abc123",
                  "productVersion": "dev",
                  "commit": "abc123",
                  "build": "20260923.1"
                }
                """));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().Be("abc123");
        result.Build.Should().Be("20260923.1");
        result.Version.Should().Be("0.7.1+abc123");
        result.ProductVersion.Should().Be("dev");
    }

    [Fact]
    public async Task FailedServiceInfo_DoesNotStampDeploymentMetadata()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: false,
                responseBody: """
                {
                  "serviceName": "TestService",
                  "version": "0.7.1+abc123",
                  "productVersion": "dev",
                  "commit": "abc123",
                  "build": "20260923.1"
                }
                """));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().BeNull();
        result.Build.Should().BeNull();
        result.Version.Should().BeNull();
        result.ProductVersion.Should().BeNull();
    }

    [Fact]
    public async Task InvalidServiceInfoJson_DoesNotStampDeploymentMetadata()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: true,
                responseBody: "{ this is not valid json }"));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().BeNull();
        result.Build.Should().BeNull();
        result.Version.Should().BeNull();
        result.ProductVersion.Should().BeNull();
    }

    [Fact]
    public async Task WhitespaceServiceInfoValues_AreStoredAsNull()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: true,
                responseBody: """
                {
                  "serviceName": "TestService",
                  "version": "   ",
                  "productVersion": "   ",
                  "commit": "   ",
                  "build": "   "
                }
                """));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().BeNull();
        result.Build.Should().BeNull();
        result.Version.Should().BeNull();
        result.ProductVersion.Should().BeNull();
    }

    [Fact]
    public async Task MissingCommit_UsesVersionSuffixAsCommit()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: true,
                responseBody: """
                {
                  "serviceName": "TestService",
                  "version": "0.7.1+abc123",
                  "productVersion": "dev",
                  "build": "20260923.1"
                }
                """));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().Be("abc123");
        result.Build.Should().Be("20260923.1");
        result.Version.Should().Be("0.7.1+abc123");
        result.ProductVersion.Should().Be("dev");
    }

    private static IReadOnlyList<ApiTestRunResult> CreateResults(
        bool passed,
        string? responseBody)
    {
        return
        [
            new ApiTestRunResult
            {
                EndpointKey = "TestService::ServiceInfo",
                ServiceName = TestServiceName,
                EndpointName = ApiEndPointLibrary.ServiceInfoGet200,
                Passed = passed,
                ExpectedStatusCode = 200,
                ActualStatusCode = passed ? 200 : 500,
                ResponseBody = responseBody
            },
            new ApiTestRunResult
            {
                EndpointKey = TestEndpointKey,
                ServiceName = TestServiceName,
                EndpointName = TestEndpointName,
                Passed = true,
                ExpectedStatusCode = 200,
                ActualStatusCode = 200
            }
        ];
    }

    private static ApiTestRunResult GetTestEndpointResult(TestApiHealthRunStore store)
    {
        return store.SavedResults.Single(
            result => result.EndpointKey == TestEndpointKey);
    }

    private static async Task<TestApiHealthRunStore> RunServiceAsync(
        IReadOnlyList<ApiTestRunResult> results)
    {
        var suite = new TestServiceSuite(results);
        var store = new TestApiHealthRunStore();

        var manager = new ApiHealthExecutionRunManager(
            new ApiEndpointRegistry([suite]),
            new TestSeedOrchestrator(),
            new ApiHealthSeedContextAccessor(),
            store,
            NullLogger<ApiHealthExecutionRunManager>.Instance);

        var runId = await manager.StartServiceAsync(TestServiceName);

        await WaitForCompletionAsync(manager, runId);

        return store;
    }

    private static async Task WaitForCompletionAsync(
        ApiHealthExecutionRunManager manager,
        Guid runId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (manager.TryGetRun(runId, out var run) && run.Completed)
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"API Health test run {runId} did not complete within the expected time.");
    }

    private sealed class TestServiceSuite(
        IReadOnlyList<ApiTestRunResult> results) : IServiceTestSuite
    {
        public string ServiceName => TestServiceName;

        public IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() => [];

        public Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(
            CancellationToken ct = default) =>
            Task.FromResult(results);
    }

    private sealed class TestSeedOrchestrator : IApiHealthSeedOrchestrator
    {
        public Task<ApiHealthSeedSession> BeginServiceAsync(
            string serviceName,
            IReadOnlyCollection<ApiHealthSeedRequirement> requirements,
            Guid? apiHealthRunId = null,
            CancellationToken ct = default) =>
            Task.FromResult(new ApiHealthSeedSession
            {
                Scope = $"Service:{serviceName}",
                Success = true
            });

        public Task<ApiHealthSeedSession> BeginAllAsync(
            IReadOnlyCollection<ApiHealthSeedRequirement> requirements,
            Guid? apiHealthRunId = null,
            CancellationToken ct = default) =>
            Task.FromResult(new ApiHealthSeedSession
            {
                Scope = "All",
                Success = true
            });

        public Task EndAsync(
            ApiHealthSeedSession session,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> IsSeedRunCancelledAsync(
            ApiHealthSeedSession session,
            CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class TestApiHealthRunStore : IApiHealthRunStore
    {
        public IReadOnlyList<ApiTestRunResult> SavedResults { get; private set; } = [];

        public Task SaveRunResultsAsync(
            IEnumerable<ApiTestRunResult> results,
            string runMode,
            DateTimeOffset startedAt,
            CancellationToken ct = default)
        {
            SavedResults = results.ToList();
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, ApiTestRunResult>>
            GetLatestResultsByServiceAsync(
                IEnumerable<string> endpointKeys,
                CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, ApiTestRunResult>());

        public Task<Dictionary<string, ApiTestRunResult>>
            GetLatestResultsForRunAsync(
                Guid runId,
                IEnumerable<string> endpointKeys,
                CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, ApiTestRunResult>());

        public Task SaveServiceRunStateAsync(
            Guid runId,
            string runMode,
            IEnumerable<string> serviceNames,
            DateTimeOffset startedAt,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<ApiHealthLatestRunContext?> GetLatestRunContextAsync(
            CancellationToken ct = default) =>
            Task.FromResult<ApiHealthLatestRunContext?>(null);

        public Task<ApiTestRunHistoryPage> GetHistoryAsync(
            string endpointKey,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default) =>
            Task.FromResult(new ApiTestRunHistoryPage());

        public Task UpsertExecutionRunStatusAsync(
            ApiHealthExecutionRunStatus status,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task AttachSeedRunAsync(
            Guid runId,
            Guid seedRunId,
            string? seedRunName,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<ApiHealthExecutionRunStatus?> GetActiveExecutionRunStatusAsync(
            CancellationToken ct = default) =>
            Task.FromResult<ApiHealthExecutionRunStatus?>(null);

        public Task<ApiHealthExecutionRunStatus?> GetLatestExecutionRunStatusAsync(
            CancellationToken ct = default) =>
            Task.FromResult<ApiHealthExecutionRunStatus?>(null);

        public Task<ApiHealthExecutionRunStatus?> GetExecutionRunStatusAsync(
            Guid runId,
            CancellationToken ct = default) =>
            Task.FromResult<ApiHealthExecutionRunStatus?>(null);

        public Task CompleteExecutionRunAsync(
            Guid runId,
            bool failed,
            string? error,
            DateTimeOffset finishedAt,
            CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}