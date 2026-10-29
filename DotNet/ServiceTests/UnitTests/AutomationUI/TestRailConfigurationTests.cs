using System.Net;
using System.Text;
using Automation.UI.Models;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.Persistence;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class TestRailOptionsBinderTests
{
    private const string PlaceholderKey = "placeholder-api-key-value";

    [Fact]
    public void Reads_quoted_project_name_and_blank_quoted_suites()
    {
        var options = Bind(new()
        {
            ["Enabled"] = "true",
            ["BaseUrl"] = "https://example.testrail.io",
            ["Username"] = "qa@example.com",
            ["ApiKey"] = PlaceholderKey,
            ["ProjectId"] = "\"Example Project 2.0\"",
            ["ScenarioSuiteId"] = "\"\"",
            ["ApiHealthSuiteId"] = "\"\""
        });

        options.Enabled.Should().BeTrue();
        options.ProjectId.Should().Be(0);
        options.ProjectName.Should().Be("Example Project 2.0");
        options.HasProject.Should().BeTrue();
        options.HasScenarioSuite.Should().BeFalse();
        options.HasApiHealthSuite.Should().BeFalse();
        options.IsConfigured.Should().BeTrue();
        options.ConfigurationProblems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("P12", 12)]
    [InlineData("p12", 12)]
    [InlineData("\"12\"", 12)]
    [InlineData(" 12 ", 12)]
    public void Reads_numeric_project_ids(string value, int expected)
    {
        var options = Bind(new() { ["ProjectId"] = value });

        options.ProjectId.Should().Be(expected);
        options.ProjectName.Should().BeEmpty();
    }

    [Theory]
    [InlineData("34", 34, "")]
    [InlineData("S34", 34, "")]
    [InlineData("\"S34\"", 34, "")]
    [InlineData("0", 0, "")]
    [InlineData("", 0, "")]
    [InlineData("Master", 0, "Master")]
    public void Reads_suite_id_or_name(string value, int expectedId, string expectedName)
    {
        var options = Bind(new() { ["ScenarioSuiteId"] = value, ["ApiHealthSuiteId"] = value });

        options.ScenarioSuiteId.Should().Be(expectedId);
        options.ScenarioSuiteName.Should().Be(expectedName);
        options.ApiHealthSuiteId.Should().Be(expectedId);
        options.ApiHealthSuiteName.Should().Be(expectedName);
    }

    [Theory]
    [InlineData("https://example.testrail.io", "https://example.testrail.io")]
    [InlineData("https://example.testrail.io/", "https://example.testrail.io")]
    [InlineData("https://example.testrail.io/index.php?/api/v2", "https://example.testrail.io")]
    [InlineData("https://example.testrail.io/index.php?/api/v2/", "https://example.testrail.io")]
    [InlineData("https://example.testrail.io/index.php", "https://example.testrail.io")]
    [InlineData("https://example.com/testrail", "https://example.com/testrail")]
    public void BaseUrl_is_reduced_to_site_root(string value, string expected)
    {
        var options = Bind(new() { ["BaseUrl"] = value });

        options.BaseUrl.Should().Be(expected);
        options.ConfigurationProblems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("example.testrail.io")]
    [InlineData("ftp://example.testrail.io")]
    [InlineData("not a url")]
    public void Invalid_BaseUrl_is_cleared_and_reported(string value)
    {
        var options = Bind(Complete(new() { ["BaseUrl"] = value }));

        options.BaseUrl.Should().BeEmpty();
        options.IsConfigured.Should().BeFalse();
        options.ConfigurationProblems.Should().ContainSingle(p => p.Contains("Automation:TestRail:BaseUrl"));
    }

    [Fact]
    public void Malformed_values_do_not_throw_and_are_reported()
    {
        var options = Bind(new()
        {
            ["Enabled"] = "yes",
            ["RequestTimeoutSeconds"] = "soon",
            ["UseSharedRun"] = "maybe",
            ["SharedScenarioRunId"] = "run-1",
            ["SkipStatusId"] = "x",
            ["ApiKey"] = PlaceholderKey,
            ["ScenarioCaseIds:Smoke"] = "C77",
            ["ScenarioCaseIds:Broken"] = "seventy",
            ["ApiHealthCaseIds:Tenant::Create"] = "\"15\""
        });

        options.Enabled.Should().BeFalse();
        options.RequestTimeoutSeconds.Should().Be(TestRailOptions.DefaultRequestTimeoutSeconds);
        options.UseSharedRun.Should().BeFalse();
        options.SharedScenarioRunId.Should().BeNull();
        options.SkipStatusId.Should().Be(0);
        options.ScenarioCaseIds.Should().Equal(new Dictionary<string, int> { ["Smoke"] = 77 });
        options.ApiHealthCaseIds.Should().Equal(new Dictionary<string, int> { ["Tenant::Create"] = 15 });
        options.ConfigurationProblems.Should().HaveCount(6);
        options.ConfigurationProblems.Should().OnlyContain(p => !p.Contains(PlaceholderKey));
    }

    [Fact]
    public void Shared_run_ids_accept_prefix()
    {
        var options = Bind(new() { ["SharedScenarioRunId"] = "R171", ["SharedApiHealthRunId"] = "172" });

        options.SharedScenarioRunId.Should().Be(171);
        options.SharedApiHealthRunId.Should().Be(172);
    }

    [Fact]
    public void Missing_section_is_disabled_with_defaults()
    {
        var options = Bind(new());

        options.Enabled.Should().BeFalse();
        options.IsConfigured.Should().BeFalse();
        options.RequestTimeoutSeconds.Should().Be(TestRailOptions.DefaultRequestTimeoutSeconds);
        options.ConfigurationProblems.Should().BeEmpty();
        options.MissingRequiredSettings().Should().Equal("BaseUrl", "Username", "ApiKey", "ProjectId");
    }

    [Fact]
    public void Shipped_appsettings_defaults_bind_to_disabled()
    {
        var options = Bind(new()
        {
            ["Enabled"] = "false",
            ["BaseUrl"] = "",
            ["ProjectId"] = "0",
            ["ScenarioSuiteId"] = "0",
            ["ApiHealthSuiteId"] = "0",
            ["RequestTimeoutSeconds"] = "15",
            ["UseSharedRun"] = "false",
            ["SkipStatusId"] = "0"
        });

        options.IsConfigured.Should().BeFalse();
        options.HasProject.Should().BeFalse();
        options.ConfigurationProblems.Should().BeEmpty();
    }

    [Fact]
    public void Options_registration_reads_through_binder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Automation:TestRail:Enabled"] = "true",
                ["Automation:TestRail:ProjectId"] = "\"Example Project\"",
                ["Automation:TestRail:ScenarioSuiteId"] = "\"\""
            })
            .Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IConfiguration>(services, configuration);
        Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions.AddOptions<TestRailOptions>(services)
            .Configure<IConfiguration>((options, config) =>
                TestRailOptionsBinder.Bind(config.GetSection(TestRailOptions.SectionName), options));

        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var options = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IOptions<TestRailOptions>>(provider).Value;

        options.ProjectName.Should().Be("Example Project");
        options.ScenarioSuiteId.Should().Be(0);
    }

    internal static Dictionary<string, string?> Complete(Dictionary<string, string?> overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["Enabled"] = "true",
            ["BaseUrl"] = "https://example.testrail.io",
            ["Username"] = "qa@example.com",
            ["ApiKey"] = PlaceholderKey,
            ["ProjectId"] = "9"
        };
        foreach (var (key, value) in overrides)
            values[key] = value;
        return values;
    }

    internal static TestRailOptions Bind(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(kv => $"{TestRailOptions.SectionName}:{kv.Key}", kv => kv.Value))
            .Build();
        return TestRailOptionsBinder.Bind(configuration.GetSection(TestRailOptions.SectionName));
    }
}

[Trait("Category", "UnitTests")]
public class TestRailTargetResolverTests
{
    [Fact]
    public async Task Numeric_project_id_needs_no_lookup()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var resolver = new TestRailTargetResolver(api.Object, NullLogger.Instance);

        var id = await resolver.ResolveProjectIdAsync(new TestRailOptions { ProjectId = 9 });

        id.Should().Be(9);
    }

    [Fact]
    public async Task Project_name_is_looked_up_once_ignoring_case()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TestRailNamedItem { Id = 3, Name = "Other" },
                new TestRailNamedItem { Id = 7, Name = "Example Project 2.0" }
            ]);
        var resolver = new TestRailTargetResolver(api.Object, NullLogger.Instance);
        var options = new TestRailOptions { ProjectName = "example project 2.0" };

        (await resolver.ResolveProjectIdAsync(options)).Should().Be(7);
        (await resolver.ResolveProjectIdAsync(options)).Should().Be(7);

        api.Verify(a => a.GetProjectsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unknown_project_name_returns_null_and_is_retried_later()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailNamedItem { Id = 3, Name = "Other" }]);
        var logger = new ListLogger();
        var resolver = new TestRailTargetResolver(api.Object, logger);
        var options = new TestRailOptions { ProjectName = "Missing" };

        (await resolver.ResolveProjectIdAsync(options)).Should().BeNull();
        (await resolver.ResolveProjectIdAsync(options)).Should().BeNull();

        api.Verify(a => a.GetProjectsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains("was not found"));
    }

    [Fact]
    public void Active_item_wins_over_completed_and_duplicates_are_ambiguous()
    {
        TestRailNamedItem[] items =
        [
            new() { Id = 1, Name = "Suite", IsCompleted = true },
            new() { Id = 2, Name = "Suite" },
            new() { Id = 3, Name = "Twice" },
            new() { Id = 4, Name = "twice" }
        ];

        TestRailTargetResolver.FindByName(items, "Suite", out _)!.Id.Should().Be(2);
        TestRailTargetResolver.FindByName(items, "Twice", out var problem).Should().BeNull();
        problem.Should().Contain("matches 2");
    }

    [Fact]
    public async Task Suite_name_is_looked_up_in_the_project()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetSuitesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailNamedItem { Id = 40, Name = "Scenarios" }]);
        var resolver = new TestRailTargetResolver(api.Object, NullLogger.Instance);

        (await resolver.ResolveSuiteIdAsync(7, 0, "Scenarios", "ScenarioSuiteId")).Should().Be(40);
        (await resolver.ResolveSuiteIdAsync(7, 0, "Scenarios", "ScenarioSuiteId")).Should().Be(40);
        (await resolver.ResolveSuiteIdAsync(7, 12, string.Empty, "ScenarioSuiteId")).Should().Be(12);
        (await resolver.ResolveSuiteIdAsync(7, 0, string.Empty, "ScenarioSuiteId")).Should().BeNull();

        api.Verify(a => a.GetSuitesAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }
}

[Trait("Category", "UnitTests")]
public class TestRailPublisherNameResolutionTests
{
    [Fact]
    public async Task Scenario_publish_uses_project_and_suite_found_by_name()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailNamedItem { Id = 7, Name = "Example Project 2.0" }]);
        api.Setup(a => a.GetSuitesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailNamedItem { Id = 40, Name = "Scenarios" }]);
        api.Setup(a => a.AddRunAsync(7, 40, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(501);
        api.Setup(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 1, CaseId = 42 }]);

        var scenarioId = Guid.NewGuid();
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = scenarioId, Name = "Smoke", TestRailCaseId = 42 });

        var options = TestRailOptionsBinderTests.Bind(TestRailOptionsBinderTests.Complete(new()
        {
            ["ProjectId"] = "\"Example Project 2.0\"",
            ["ScenarioSuiteId"] = "Scenarios"
        }));
        var publisher = new TestRailPublisher(
            Options.Create(options),
            api.Object,
            scenarios.Object,
            Mock.Of<IApiHealthRunStore>(),
            new ApiEndpointRegistry([]),
            NullLogger<TestRailPublisher>.Instance);

        await publisher.PublishScenarioRunAsync(new ScenarioTestRailPublishRequest
        {
            RunId = Guid.NewGuid(),
            ScenarioId = scenarioId,
            RunName = "Smoke",
            Status = AutomationRunStatus.Succeeded,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-3),
            FinishedAt = DateTimeOffset.UtcNow
        });

        api.Verify(a => a.AddRunAsync(7, 40, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Blank_suite_skips_without_calling_TestRail()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var options = TestRailOptionsBinderTests.Bind(TestRailOptionsBinderTests.Complete(new()
        {
            ["ProjectId"] = "\"Example Project 2.0\"",
            ["ScenarioSuiteId"] = "\"\"",
            ["ApiHealthSuiteId"] = "\"\""
        }));
        var publisher = new TestRailPublisher(
            Options.Create(options),
            api.Object,
            Mock.Of<IScenarioStore>(),
            Mock.Of<IApiHealthRunStore>(),
            new ApiEndpointRegistry([]),
            NullLogger<TestRailPublisher>.Instance);

        await publisher.PublishScenarioRunAsync(new ScenarioTestRailPublishRequest
        {
            RunId = Guid.NewGuid(),
            Status = AutomationRunStatus.Failed
        });
        await publisher.PublishApiHealthRunAsync(new ApiHealthTestRailPublishRequest
        {
            RunId = Guid.NewGuid(),
            Scope = "All"
        });

        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Unknown_project_name_logs_error_and_posts_nothing()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetProjectsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var scenarioId = Guid.NewGuid();
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = scenarioId, Name = "Smoke", TestRailCaseId = 42 });
        var options = TestRailOptionsBinderTests.Bind(TestRailOptionsBinderTests.Complete(new()
        {
            ["ProjectId"] = "Nope",
            ["ScenarioSuiteId"] = "40"
        }));
        var publisher = new TestRailPublisher(
            Options.Create(options),
            api.Object,
            scenarios.Object,
            Mock.Of<IApiHealthRunStore>(),
            new ApiEndpointRegistry([]),
            NullLogger<TestRailPublisher>.Instance);

        await publisher.PublishScenarioRunAsync(new ScenarioTestRailPublishRequest
        {
            RunId = Guid.NewGuid(),
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Succeeded
        });

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

[Trait("Category", "UnitTests")]
public class TestRailApiClientListTests
{
    [Fact]
    public async Task GetProjects_follows_pages()
    {
        var handler = new PathHandler(path => path switch
        {
            _ when path.EndsWith("get_projects", StringComparison.Ordinal) =>
                """{"offset":0,"limit":2,"size":2,"_links":{"next":"/api/v2/get_projects&offset=2","prev":null},"projects":[{"id":1,"name":"A","is_completed":false},{"id":2,"name":"B","is_completed":true}]}""",
            _ when path.EndsWith("get_projects&offset=2", StringComparison.Ordinal) =>
                """{"offset":2,"limit":2,"size":1,"_links":{"next":null,"prev":null},"projects":[{"id":3,"name":"C"}]}""",
            _ => throw new InvalidOperationException(path)
        });
        var client = CreateClient(handler);

        var projects = await client.GetProjectsAsync();

        projects.Select(p => p.Id).Should().Equal(1, 2, 3);
        projects[1].IsCompleted.Should().BeTrue();
        handler.Paths.Should().HaveCount(2);
        handler.Methods.Should().OnlyContain(m => m == HttpMethod.Get);
    }

    [Fact]
    public async Task GetProjects_reads_bare_array()
    {
        var handler = new PathHandler(_ => """[{"id":5,"name":"Legacy"}]""");
        var client = CreateClient(handler);

        var projects = await client.GetProjectsAsync();

        projects.Should().ContainSingle(p => p.Id == 5 && p.Name == "Legacy");
    }

    [Fact]
    public async Task GetSuites_reads_project_suites()
    {
        var handler = new PathHandler(_ => """[{"id":40,"name":"Scenarios"},{"id":41,"name":"API Health","is_completed":false}]""");
        var client = CreateClient(handler);

        var suites = await client.GetSuitesAsync(7);

        suites.Select(s => s.Id).Should().Equal(40, 41);
        handler.Paths.Should().ContainSingle().Which.Should().Be("https://example.testrail.io/index.php?/api/v2/get_suites/7");
    }

    [Fact]
    public async Task GetSuites_follows_pages()
    {
        var handler = new PathHandler(path => path switch
        {
            _ when path.EndsWith("get_suites/7", StringComparison.Ordinal) =>
                """{"offset":0,"limit":2,"size":2,"_links":{"next":"/api/v2/get_suites/7&offset=2","prev":null},"suites":[{"id":40,"name":"Scenarios"},{"id":41,"name":"API Health"}]}""",
            _ when path.EndsWith("get_suites/7&offset=2", StringComparison.Ordinal) =>
                """{"offset":2,"limit":2,"size":1,"_links":{"next":null,"prev":null},"suites":[{"id":42,"name":"Other"}]}""",
            _ => throw new InvalidOperationException(path)
        });
        var client = CreateClient(handler);

        var suites = await client.GetSuitesAsync(7);

        suites.Select(s => s.Id).Should().Equal(40, 41, 42);
        handler.Paths.Should().HaveCount(2);
    }

    [Fact]
    public async Task Unauthorized_list_throws_with_status()
    {
        var handler = new PathHandler(_ => """{"error":"Authentication failed"}""", HttpStatusCode.Unauthorized);
        var client = CreateClient(handler);

        await client.Invoking(c => c.GetProjectsAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*401*");
    }

    private static TestRailApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new TestRailOptions
        {
            BaseUrl = "https://example.testrail.io",
            Username = "qa@example.com",
            ApiKey = "placeholder-key"
        }, NullLogger<TestRailApiClient>.Instance);

    private sealed class PathHandler(Func<string, string> respond, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<HttpMethod> Methods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.ToString();
            Paths.Add(path);
            Methods.Add(request.Method);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(respond(path), Encoding.UTF8, "application/json")
            });
        }
    }
}

[Trait("Category", "UnitTests")]
public class TestRailStartupTests
{
    private const string PlaceholderKey = "placeholder-api-key-value";

    [Fact]
    public void Startup_report_never_logs_user_or_key()
    {
        var options = TestRailOptionsBinderTests.Bind(TestRailOptionsBinderTests.Complete(new()
        {
            ["Username"] = "someone@example.com",
            ["ApiKey"] = PlaceholderKey,
            ["ProjectId"] = "\"Example Project 2.0\"",
            ["ScenarioSuiteId"] = "\"\""
        }));
        var logger = new ListLogger();

        TestRailStartupReport.Log(logger, options);

        logger.Entries[0].Message.Should().Contain("TestRail publishing is enabled.")
            .And.Contain("\"Example Project 2.0\" (looked up by name)")
            .And.Contain("Username set")
            .And.Contain("ApiKey set");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("ScenarioSuiteId is not set"));
        logger.Entries.Should().OnlyContain(e => !e.Message.Contains(PlaceholderKey) && !e.Message.Contains("someone@example.com"));
    }

    [Fact]
    public void Startup_report_lists_missing_settings_when_enabled()
    {
        var options = TestRailOptionsBinderTests.Bind(new() { ["Enabled"] = "true", ["BaseUrl"] = "https://example.testrail.io" });
        var logger = new ListLogger();

        TestRailStartupReport.Log(logger, options);

        logger.Entries[0].Message.Should().Contain("enabled but incomplete, so off");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning
            && e.Message.Contains("Automation:TestRail:Username")
            && e.Message.Contains("Automation:TestRail:ApiKey")
            && e.Message.Contains("Automation:TestRail:ProjectId"));
    }

    [Fact]
    public void Startup_report_is_one_line_when_disabled()
    {
        var logger = new ListLogger();

        TestRailStartupReport.Log(logger, TestRailOptionsBinderTests.Bind(new()));

        logger.Entries.Should().ContainSingle().Which.Message.Should().Contain("TestRail publishing is disabled.");
    }

    [Fact]
    public async Task Startup_check_resolves_project_and_lists_suites_when_unset()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailNamedItem { Id = 7, Name = "Example Project 2.0" }]);
        api.Setup(a => a.GetSuitesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailNamedItem { Id = 40, Name = "Scenarios" }, new TestRailNamedItem { Id = 41, Name = "API Health" }]);
        var options = TestRailOptionsBinderTests.Bind(TestRailOptionsBinderTests.Complete(new()
        {
            ["ProjectId"] = "\"Example Project 2.0\"",
            ["ApiHealthSuiteId"] = "S41"
        }));
        var logger = new ListLogger<TestRailStartupCheckService>();
        var check = new TestRailStartupCheckService(Options.Create(options), api.Object, logger);

        await check.RunCheckAsync(CancellationToken.None);

        logger.Entries.Should().Contain(e => e.Message.Contains("connection check passed") && e.Message.Contains("project 7"));
        logger.Entries.Should().Contain(e => e.Message.Contains("ScenarioSuiteId is not set") && e.Message.Contains("S40 Scenarios; S41 API Health"));
        logger.Entries.Should().Contain(e => e.Message.Contains("ApiHealthSuiteId is suite 41"));
        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Startup_check_reports_unknown_suite()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetSuitesAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailNamedItem { Id = 40, Name = "Scenarios" }]);
        var options = TestRailOptionsBinderTests.Bind(TestRailOptionsBinderTests.Complete(new() { ["ScenarioSuiteId"] = "99" }));
        var logger = new ListLogger<TestRailStartupCheckService>();
        var check = new TestRailStartupCheckService(Options.Create(options), api.Object, logger);

        await check.RunCheckAsync(CancellationToken.None);

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("ScenarioSuiteId 99 is not a suite"));
    }

    [Fact]
    public async Task Startup_check_failure_is_a_warning_only()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetSuitesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("No such host is known."));
        var options = TestRailOptionsBinderTests.Bind(TestRailOptionsBinderTests.Complete(new()));
        var logger = new ListLogger<TestRailStartupCheckService>();
        var check = new TestRailStartupCheckService(Options.Create(options), api.Object, logger);

        await check.Invoking(c => c.RunCheckAsync(CancellationToken.None)).Should().NotThrowAsync();

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning
            && e.Message.Contains("connection check failed")
            && e.Message.Contains("No such host"));
    }

    [Fact]
    public async Task Startup_check_does_nothing_when_not_configured()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var check = new TestRailStartupCheckService(
            Options.Create(TestRailOptionsBinderTests.Bind(new())),
            api.Object,
            NullLogger<TestRailStartupCheckService>.Instance);

        await check.RunCheckAsync(CancellationToken.None);

        api.VerifyNoOtherCalls();
    }
}

internal class ListLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}

internal sealed class ListLogger<T> : ListLogger, ILogger<T>;
