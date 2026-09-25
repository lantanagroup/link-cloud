using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ApiHealthExecutionRunManagerTests
{
    private const string TestServiceName = "TestService";
    private const string TestEndpointName = "Test Endpoint";
    private const string TestEndpointKey = "TestService::TestEndpoint";

    [Fact]
    public async Task RunAll_HostedSuitesUseMetadataWithoutDependingOnHostSuiteOrder()
    {
        var dmrpResults = CreateHostedResults(
            ApiEndPointLibrary.ServiceNames.Dmrp,
            ApiEndPointLibrary.DmrpSteps.MappingPost201);

        var adminBffAuthResults = CreateHostedResults(
            ApiEndPointLibrary.ServiceNames.AdminBffAuth,
            "Admin BFF Auth Step");

        var tenantResults = CreateServiceResults(
            ApiEndPointLibrary.ServiceNames.Tenant,
            """
        {
          "serviceName": "Tenant",
          "version": "0.7.1+tenant123",
          "productVersion": "tenant-dev",
          "commit": "tenant123",
          "build": "tenant-build"
        }
        """);

        var adminBffResults = CreateServiceResults(
            ApiEndPointLibrary.ServiceNames.AdminBff,
            """
        [
          {
            "serviceName": "Link Admin BFF",
            "version": "0.7.1+admin123",
            "productVersion": "admin-dev",
            "commit": "admin123",
            "build": "admin-build"
          }
        ]
        """);

        var httpClientFactory = CreateHostMetadataHttpClientFactory();

        var store = await RunAllAsync(
            httpClientFactory,
            new TestServiceSuite(
                ApiEndPointLibrary.ServiceNames.Dmrp,
                dmrpResults),
            new TestServiceSuite(
                ApiEndPointLibrary.ServiceNames.AdminBffAuth,
                adminBffAuthResults),
            new TestServiceSuite(
                ApiEndPointLibrary.ServiceNames.Tenant,
                tenantResults),
            new TestServiceSuite(
                ApiEndPointLibrary.ServiceNames.AdminBff,
                adminBffResults));

        var dmrpResult = store.SavedResults.Single(
            result => result.ServiceName == ApiEndPointLibrary.ServiceNames.Dmrp);

        dmrpResult.Commit.Should().Be("tenant123");
        dmrpResult.Build.Should().Be("tenant-build");
        dmrpResult.Version.Should().Be("0.7.1+tenant123");
        dmrpResult.ProductVersion.Should().Be("tenant-dev");

        var adminBffAuthResult = store.SavedResults.Single(
            result => result.ServiceName == ApiEndPointLibrary.ServiceNames.AdminBffAuth);

        adminBffAuthResult.Commit.Should().Be("admin123");
        adminBffAuthResult.Build.Should().Be("admin-build");
        adminBffAuthResult.Version.Should().Be("0.7.1+admin123");
        adminBffAuthResult.ProductVersion.Should().Be("admin-dev");
    }

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

    [Fact]
    public async Task AdminBffServiceInfoArray_UsesFirstObjectMetadata()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: true,
                responseBody: """
            [
              {
                "serviceName": "Link Admin BFF",
                "version": "0.7.1+admin123",
                "productVersion": "admin-dev",
                "commit": "admin123",
                "build": "admin-build"
              },
              {
                "serviceName": "Account",
                "version": "0.7.1+account456",
                "productVersion": "account-dev",
                "commit": "account456",
                "build": "account-build"
              }
            ]
            """));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().Be("admin123");
        result.Build.Should().Be("admin-build");
        result.Version.Should().Be("0.7.1+admin123");
        result.ProductVersion.Should().Be("admin-dev");
    }

    [Fact]
    public async Task VersionWithoutSuffix_DoesNotUseVersionAsCommit()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: true,
                responseBody: """
            {
              "serviceName": "TestService",
              "version": "0.7.1",
              "productVersion": "dev",
              "build": "20260925.1"
            }
            """));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().BeNull();
        result.Build.Should().Be("20260925.1");
        result.Version.Should().Be("0.7.1");
        result.ProductVersion.Should().Be("dev");
    }

    [Fact]
    public async Task NoServiceInfoStep_DoesNotStampDeploymentMetadata()
    {
        var results = new List<ApiTestRunResult>
    {
        new()
        {
            EndpointKey = TestEndpointKey,
            ServiceName = TestServiceName,
            EndpointName = TestEndpointName,
            Passed = true,
            ExpectedStatusCode = 200,
            ActualStatusCode = 200
        }
    };

        var store = await RunServiceAsync(results);

        var result = GetTestEndpointResult(store);

        result.Commit.Should().BeNull();
        result.Build.Should().BeNull();
        result.Version.Should().BeNull();
        result.ProductVersion.Should().BeNull();
    }

    [Fact]
    public async Task ExplicitCommit_DoesNotUseDifferentVersionSuffix()
    {
        var store = await RunServiceAsync(
            CreateResults(
                passed: true,
                responseBody: """
            {
              "serviceName": "TestService",
              "version": "0.7.1+suffix456",
              "productVersion": "dev",
              "commit": "explicit123",
              "build": "20260925.1"
            }
            """));

        var result = GetTestEndpointResult(store);

        result.Commit.Should().Be("explicit123");
        result.Build.Should().Be("20260925.1");
        result.Version.Should().Be("0.7.1+suffix456");
        result.ProductVersion.Should().Be("dev");
    }

    [Fact]
    public async Task DmrpOnlyRun_FetchesTenantMetadataFromHostService()
    {
        var suite = new TestServiceSuite(
            ApiEndPointLibrary.ServiceNames.Dmrp,
            CreateHostedResults(
                ApiEndPointLibrary.ServiceNames.Dmrp,
                ApiEndPointLibrary.DmrpSteps.MappingPost201));

        var store = new TestApiHealthRunStore();

        var manager = new ApiHealthExecutionRunManager(
            new ApiEndpointRegistry([suite]),
            new TestSeedOrchestrator(),
            new ApiHealthSeedContextAccessor(),
            store,
            CreateHostMetadataHttpClientFactory(),
            CreateConfiguration(),
            NullLogger<ApiHealthExecutionRunManager>.Instance);

        var runId = await manager.StartServiceAsync(
            ApiEndPointLibrary.ServiceNames.Dmrp);

        await WaitForCompletionAsync(manager, runId);

        var result = store.SavedResults.Single();

        result.Commit.Should().Be("tenant123");
        result.Build.Should().Be("tenant-build");
        result.Version.Should().Be("0.7.1+tenant123");
        result.ProductVersion.Should().Be("tenant-dev");
    }

    [Fact]
    public async Task AdminBffAuthOnlyRun_FetchesAdminBffMetadataFromHostService()
    {
        var suite = new TestServiceSuite(
            ApiEndPointLibrary.ServiceNames.AdminBffAuth,
            CreateHostedResults(
                ApiEndPointLibrary.ServiceNames.AdminBffAuth,
                "Admin BFF Auth Step"));

        var store = new TestApiHealthRunStore();

        var manager = new ApiHealthExecutionRunManager(
            new ApiEndpointRegistry([suite]),
            new TestSeedOrchestrator(),
            new ApiHealthSeedContextAccessor(),
            store,
            CreateHostMetadataHttpClientFactory(),
            CreateConfiguration(),
            NullLogger<ApiHealthExecutionRunManager>.Instance);

        var runId = await manager.StartServiceAsync(
            ApiEndPointLibrary.ServiceNames.AdminBffAuth);

        await WaitForCompletionAsync(manager, runId);

        var result = store.SavedResults.Single();

        result.Commit.Should().Be("admin123");
        result.Build.Should().Be("admin-build");
        result.Version.Should().Be("0.7.1+admin123");
        result.ProductVersion.Should().Be("admin-dev");
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

    private static async Task<TestApiHealthRunStore> RunServiceAsync(IReadOnlyList<ApiTestRunResult> results)
    {
        var suite = new TestServiceSuite(TestServiceName, results);
        var store = new TestApiHealthRunStore();

        var manager = new ApiHealthExecutionRunManager(
            new ApiEndpointRegistry([suite]),
            new TestSeedOrchestrator(),
            new ApiHealthSeedContextAccessor(),
            store,
            CreateDefaultHttpClientFactory(),
            CreateConfiguration(),
            NullLogger<ApiHealthExecutionRunManager>.Instance);

        var runId = await manager.StartServiceAsync(TestServiceName);

        await WaitForCompletionAsync(manager, runId);

        return store;
    }

    private static async Task<TestApiHealthRunStore> RunAllAsync(IHttpClientFactory httpClientFactory, params IServiceTestSuite[] suites)
    {
        var store = new TestApiHealthRunStore();

        var manager = new ApiHealthExecutionRunManager(
            new ApiEndpointRegistry(suites),
            new TestSeedOrchestrator(),
            new ApiHealthSeedContextAccessor(),
            store,
            httpClientFactory,
            CreateConfiguration(),
            NullLogger<ApiHealthExecutionRunManager>.Instance);

        var runId = await manager.StartAllAsync();

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

    private static IReadOnlyList<ApiTestRunResult> CreateServiceResults(
    string serviceName,
    string responseBody)
    {
        return
        [
            new ApiTestRunResult
        {
            EndpointKey = $"{serviceName}::ServiceInfo",
            ServiceName = serviceName,
            EndpointName = ApiEndPointLibrary.ServiceInfoGet200,
            Passed = true,
            ExpectedStatusCode = 200,
            ActualStatusCode = 200,
            ResponseBody = responseBody
        },
        new ApiTestRunResult
        {
            EndpointKey = $"{serviceName}::TestEndpoint",
            ServiceName = serviceName,
            EndpointName = "Test Endpoint",
            Passed = true,
            ExpectedStatusCode = 200,
            ActualStatusCode = 200
        }
        ];
    }

    private static IReadOnlyList<ApiTestRunResult> CreateHostedResults(
        string serviceName,
        string endpointName)
    {
        return
        [
            new ApiTestRunResult
        {
            EndpointKey = $"{serviceName}::{endpointName}",
            ServiceName = serviceName,
            EndpointName = endpointName,
            Passed = true,
            ExpectedStatusCode = 200,
            ActualStatusCode = 200
        }
        ];
    }

    private sealed class TestServiceSuite(string serviceName, IReadOnlyList<ApiTestRunResult> results) : IServiceTestSuite
    {
        public string ServiceName => serviceName;

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
        private readonly List<ApiTestRunResult> _savedResults = [];

        public IReadOnlyList<ApiTestRunResult> SavedResults => _savedResults;

        public Task SaveRunResultsAsync(
            IEnumerable<ApiTestRunResult> results,
            string runMode,
            DateTimeOffset startedAt,
            CancellationToken ct = default)
        {
            _savedResults.AddRange(results);
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

    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceRegistry:TenantServiceApiUrl"] = "http://tenant.test/api",
                ["ServiceRegistry:AdminBffServiceUrl"] = "http://adminbff.test"
            })
            .Build();
    }

    private static IHttpClientFactory CreateDefaultHttpClientFactory()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        return new TestHttpClientFactory(new HttpClient(handler));
    }

    private static IHttpClientFactory CreateHostMetadataHttpClientFactory()
    {
        var handler = new TestHttpMessageHandler(request =>
        {
            var uri = request.RequestUri?.ToString();

            if (uri == "http://tenant.test/api/facility/info")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                    {
                      "serviceName": "Tenant",
                      "version": "0.7.1+tenant123",
                      "productVersion": "tenant-dev",
                      "commit": "tenant123",
                      "build": "tenant-build"
                    }
                    """)
                };
            }

            if (uri == "http://adminbff.test/api/info")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                    [
                      {
                        "serviceName": "Link Admin BFF",
                        "version": "0.7.1+admin123",
                        "productVersion": "admin-dev",
                        "commit": "admin123",
                        "build": "admin-build"
                      },
                      {
                        "serviceName": "Account",
                        "version": "0.7.1+wrong456",
                        "productVersion": "wrong-dev",
                        "commit": "wrong456",
                        "build": "wrong-build"
                      }
                    ]
                    """)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        return new TestHttpClientFactory(new HttpClient(handler));
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}