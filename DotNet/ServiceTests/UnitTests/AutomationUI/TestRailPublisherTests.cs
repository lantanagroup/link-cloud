using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class TestRailStatusMapperTests
{
    [Theory]
    [InlineData(AutomationRunStatus.Succeeded, TestRailStatusMapper.Passed)]
    [InlineData(AutomationRunStatus.Failed, TestRailStatusMapper.Failed)]
    [InlineData(AutomationRunStatus.Cancelled, TestRailStatusMapper.Blocked)]
    [InlineData(AutomationRunStatus.Running, TestRailStatusMapper.Failed)]
    public void FromScenarioStatus_maps_known_outcomes(AutomationRunStatus status, int expected)
        => TestRailStatusMapper.FromScenarioStatus(status).Should().Be(expected);

    [Fact]
    public void FromApiHealthResult_maps_pass_and_fail()
    {
        TestRailStatusMapper.FromApiHealthResult(passed: true, skipped: false, skipStatusId: 6)
            .Should().Be(TestRailStatusMapper.Passed);
        TestRailStatusMapper.FromApiHealthResult(passed: false, skipped: false, skipStatusId: 6)
            .Should().Be(TestRailStatusMapper.Failed);
    }

    [Fact]
    public void FromApiHealthResult_omits_skip_when_skip_status_is_unset()
        => TestRailStatusMapper.FromApiHealthResult(passed: false, skipped: true, skipStatusId: 0)
            .Should().BeNull();

    [Fact]
    public void FromApiHealthResult_uses_configured_skip_status()
        => TestRailStatusMapper.FromApiHealthResult(passed: false, skipped: true, skipStatusId: 6)
            .Should().Be(6);
}

[Trait("Category", "UnitTests")]
public class TestRailCaseMapperTests
{
    [Fact]
    public void Scenario_property_wins_over_mapping_config()
    {
        var map = new Dictionary<string, int> { ["alpha"] = 99 };
        TestRailCaseMapper.ResolveScenarioCaseId(42, Guid.NewGuid(), "alpha", map)
            .Should().Be(42);
    }

    [Fact]
    public void Scenario_map_matches_id_then_name()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var map = new Dictionary<string, int>
        {
            [id.ToString()] = 11,
            ["Smoke"] = 22
        };

        TestRailCaseMapper.ResolveScenarioCaseId(null, id, "other", map).Should().Be(11);
        TestRailCaseMapper.ResolveScenarioCaseId(null, Guid.NewGuid(), "Smoke", map).Should().Be(22);
    }

    [Fact]
    public void Missing_or_zero_mapping_returns_null()
    {
        TestRailCaseMapper.ResolveScenarioCaseId(null, Guid.NewGuid(), "none", new Dictionary<string, int>())
            .Should().BeNull();
        TestRailCaseMapper.ResolveScenarioCaseId(0, null, "none", new Dictionary<string, int> { ["none"] = 0 })
            .Should().BeNull();
    }

    [Fact]
    public void ApiHealth_property_wins_over_endpoint_key_map()
    {
        var map = new Dictionary<string, int> { ["Tenant::Create"] = 7 };
        TestRailCaseMapper.ResolveApiHealthCaseId(15, "Tenant::Create", "Create", map)
            .Should().Be(15);
        TestRailCaseMapper.ResolveApiHealthCaseId(null, "Tenant::Create", "Create", map)
            .Should().Be(7);
    }
}

[Trait("Category", "UnitTests")]
public class TestRailPublisherTests
{
    [Fact]
    public async Task Disabled_or_incomplete_config_is_noop()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var publisher = CreatePublisher(api.Object, new TestRailOptions { Enabled = false });

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());
        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_case_id_is_noop()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var publisher = CreatePublisher(api.Object, ConfiguredOptions());

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());

        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Scenario_success_creates_run_and_posts_passed_result()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(501);
        api.Setup(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 1, CaseId = 42 }]);

        var scenarioId = Guid.NewGuid();
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = scenarioId, Name = "Smoke", TestRailCaseId = 42 });

        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), scenarios.Object);
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Succeeded
        });

        api.Verify(a => a.AddRunAsync(
            9,
            100,
            It.Is<string>(n => n.Contains("Smoke", StringComparison.Ordinal)),
            It.Is<IReadOnlyList<int>>(ids => ids.Single() == 42),
            It.IsAny<CancellationToken>()), Times.Once);

        api.Verify(a => a.AddResultsForCasesAsync(
            501,
            It.Is<IReadOnlyList<TestRailCaseResult>>(r =>
                r.Count == 1
                && r[0].CaseId == 42
                && r[0].StatusId == TestRailStatusMapper.Passed
                && r[0].Attachment == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scenario_failure_attaches_logs()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);
        api.Setup(a => a.AddResultsForCasesAsync(8, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 77, CaseId = 42 }]);
        api.Setup(a => a.AddAttachmentToResultAsync(77, It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var opts = ConfiguredOptions();
        opts.ScenarioCaseIds["Smoke"] = 42;
        var publisher = CreatePublisher(api.Object, opts);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            Status = AutomationRunStatus.Failed,
            Error = "boom",
            Logs = ["line 1", "line 2"]
        });

        api.Verify(a => a.AddAttachmentToResultAsync(
            77,
            It.Is<string>(n => n.EndsWith(".log", StringComparison.Ordinal)),
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains("boom")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UseSharedRun_creates_once_then_reuses()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321);
        api.Setup(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        opts.ScenarioCaseIds["Smoke"] = 42;
        var publisher = CreatePublisher(api.Object, opts);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SharedRunId_skips_add_run()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddResultsForCasesAsync(999, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        opts.SharedScenarioRunId = 999;
        opts.ScenarioCaseIds["Smoke"] = 42;
        var publisher = CreatePublisher(api.Object, opts);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddResultsForCasesAsync(999, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Fail_open_when_add_run_throws()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("testrail down"));

        var opts = ConfiguredOptions();
        opts.ScenarioCaseIds["Smoke"] = 42;
        var publisher = CreatePublisher(api.Object, opts);

        var act = () => publisher.PublishScenarioRunAsync(SampleScenarioRequest());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Fail_open_when_add_results_throws()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        api.Setup(a => a.AddResultsForCasesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("500"));

        var opts = ConfiguredOptions();
        opts.ScenarioCaseIds["Smoke"] = 42;
        var publisher = CreatePublisher(api.Object, opts);

        await publisher.Invoking(p => p.PublishScenarioRunAsync(SampleScenarioRequest()))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApiHealth_maps_pass_fail_skip_and_creates_run()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 200, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(44);
        api.Setup(a => a.AddResultsForCasesAsync(44, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var passDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Create Facility",
            TestRailCaseId = 1
        };
        var failDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Get Facility",
            TestRailCaseId = 2
        };
        var skipDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Skip Me",
            TestRailCaseId = 3
        };

        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [passDef.Key] = new() { EndpointKey = passDef.Key, EndpointName = passDef.EndpointName, ServiceName = "Tenant", Passed = true, DurationMs = 1500 },
                [failDef.Key] = new() { EndpointKey = failDef.Key, EndpointName = failDef.EndpointName, ServiceName = "Tenant", Passed = false, ErrorMessage = "nope" },
                [skipDef.Key] = new() { EndpointKey = skipDef.Key, EndpointName = skipDef.EndpointName, ServiceName = "Tenant", Skipped = true, SkipReason = "env" }
            });

        var opts = ConfiguredOptions();
        opts.SkipStatusId = 6;
        var publisher = CreatePublisher(
            api.Object,
            opts,
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(passDef, failDef, skipDef)]));

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        api.Verify(a => a.AddResultsForCasesAsync(
            44,
            It.Is<IReadOnlyList<TestRailCaseResult>>(results =>
                results.Count == 3
                && results.Single(r => r.CaseId == 1).StatusId == TestRailStatusMapper.Passed
                && results.Single(r => r.CaseId == 2).StatusId == TestRailStatusMapper.Failed
                && results.Single(r => r.CaseId == 3).StatusId == 6),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApiHealth_omits_skipped_when_skip_status_unset()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        api.Setup(a => a.AddResultsForCasesAsync(3, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var skipDef = new ApiEndpointDefinition { ServiceName = "Tenant", EndpointName = "Skip Me", TestRailCaseId = 3 };
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [skipDef.Key] = new() { EndpointKey = skipDef.Key, EndpointName = skipDef.EndpointName, Skipped = true }
            });

        var publisher = CreatePublisher(
            api.Object,
            ConfiguredOptions(),
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(skipDef)]));

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApiHealth_fail_open_when_store_throws()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mongo down"));

        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), apiHealthStore: store.Object);
        await publisher.Invoking(p => p.PublishApiHealthRunAsync(SampleApiHealthRequest()))
            .Should().NotThrowAsync();
        api.VerifyNoOtherCalls();
    }

    [Fact]
    public void FormatElapsedMs_matches_testrail_style()
    {
        TestRailPublisher.FormatElapsedMs(400).Should().Be("1s");
        TestRailPublisher.FormatElapsedMs(1500).Should().Be("2s");
        TestRailPublisher.FormatElapsedMs(60_000).Should().Be("1m");
        TestRailPublisher.FormatElapsedMs(90_000).Should().Be("1m 30s");
    }

    private static TestRailPublisher CreatePublisher(
        ITestRailApiClient api,
        TestRailOptions opts,
        IScenarioStore? scenarios = null,
        IApiHealthRunStore? apiHealthStore = null,
        ApiEndpointRegistry? registry = null)
    {
        return new TestRailPublisher(
            Options.Create(opts),
            api,
            scenarios ?? Mock.Of<IScenarioStore>(),
            apiHealthStore ?? Mock.Of<IApiHealthRunStore>(),
            registry ?? new ApiEndpointRegistry([]),
            NullLogger<TestRailPublisher>.Instance);
    }

    private static TestRailOptions ConfiguredOptions() => new()
    {
        Enabled = true,
        BaseUrl = "https://example.testrail.io",
        Username = "qa@example.com",
        ApiKey = "placeholder-key",
        ProjectId = 9,
        ScenarioSuiteId = 100,
        ApiHealthSuiteId = 200
    };

    private static ScenarioTestRailPublishRequest SampleScenarioRequest() => new()
    {
        RunId = Guid.NewGuid(),
        RunName = "Smoke",
        Status = AutomationRunStatus.Succeeded,
        StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
        FinishedAt = DateTimeOffset.UtcNow
    };

    private static ApiHealthTestRailPublishRequest SampleApiHealthRequest() => new()
    {
        RunId = Guid.NewGuid(),
        Scope = "Service",
        ServiceName = "Tenant",
        StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
        FinishedAt = DateTimeOffset.UtcNow
    };

    private sealed class FakeSuite(params ApiEndpointDefinition[] defs) : IServiceTestSuite
    {
        public string ServiceName => defs.FirstOrDefault()?.ServiceName ?? "Fake";
        public IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() => defs;
        public Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApiTestRunResult>>([]);
        public IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() => [];
    }
}

[Trait("Category", "UnitTests")]
public class TestRailApiClientTests
{
    [Fact]
    public async Task AddRun_posts_basic_auth_and_case_ids()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(HttpStatusCode.OK, """{"id": 55}""")
        };
        var client = CreateClient(handler);

        var runId = await client.AddRunAsync(9, 100, "run-name", [1, 2]);

        runId.Should().Be(55);
        handler.Requests.Should().ContainSingle();
        var (request, body) = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://example.testrail.io/index.php?/api/v2/add_run/9");
        request.Headers.Authorization.Should().BeEquivalentTo(new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes("qa@example.com:placeholder-key"))));

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("suite_id").GetInt32().Should().Be(100);
        doc.RootElement.GetProperty("include_all").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("case_ids").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(1, 2);
    }

    [Fact]
    public async Task AddResultsForCases_posts_status_map()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(HttpStatusCode.OK, """[{"id": 10, "case_id": 1}]""")
        };
        var client = CreateClient(handler);

        var posted = await client.AddResultsForCasesAsync(55,
        [
            new TestRailCaseResult { CaseId = 1, StatusId = 5, Comment = "failed" }
        ]);

        posted.Should().ContainSingle(r => r.Id == 10 && r.CaseId == 1);
        var body = handler.Requests[0].Body;
        using var doc = JsonDocument.Parse(body);
        var result = doc.RootElement.GetProperty("results")[0];
        result.GetProperty("case_id").GetInt32().Should().Be(1);
        result.GetProperty("status_id").GetInt32().Should().Be(5);
        result.GetProperty("comment").GetString().Should().Be("failed");
    }

    [Fact]
    public async Task Non_success_throws()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(HttpStatusCode.InternalServerError, """{"error": "nope"}""")
        };
        var client = CreateClient(handler);

        await client.Invoking(c => c.AddRunAsync(9, 100, "x", [1]))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*500*");
    }

    [Fact]
    public async Task AddAttachment_sends_multipart()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"attachment_id": 1}""", Encoding.UTF8, "application/json")
            }
        };
        var client = CreateClient(handler);

        await client.AddAttachmentToResultAsync(10, "fail.log", "hello"u8.ToArray());

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Request.RequestUri!.ToString()
            .Should().Be("https://example.testrail.io/index.php?/api/v2/add_attachment_to_result/10");
        handler.Requests[0].Request.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
    }

    private static TestRailApiClient CreateClient(ScriptedHandler handler)
    {
        var http = new HttpClient(handler);
        var options = new TestRailOptions
        {
            BaseUrl = "https://example.testrail.io",
            Username = "qa@example.com",
            ApiKey = "placeholder-key"
        };
        return new TestRailApiClient(http, options, NullLogger<TestRailApiClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];
        public Func<HttpRequestMessage, string, HttpResponseMessage> Responder { get; set; } =
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return Responder(request, body);
        }
    }
}
