using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class ApiHealthSuitePassTests
{
    private static readonly IReadOnlySet<string> NoInformational = new HashSet<string>();

    [Fact]
    public void A_skipped_step_does_not_keep_the_seed_facility()
    {
        var results = new[]
        {
            new ApiTestRunResult { EndpointKey = "Tenant::Health", Passed = true },
            new ApiTestRunResult { EndpointKey = "Account::MissingConfig", Skipped = true }
        };

        ApiHealthExecutionRunManager.SuiteChecksPassed(results, NoInformational).Should().BeTrue();
    }

    [Fact]
    public void A_failed_check_keeps_the_seed_facility()
    {
        var results = new[]
        {
            new ApiTestRunResult { EndpointKey = "Tenant::Health", Passed = false }
        };

        ApiHealthExecutionRunManager.SuiteChecksPassed(results, NoInformational).Should().BeFalse();
    }

    [Fact]
    public void An_informational_miss_does_not_keep_the_seed_facility()
    {
        var informational = new HashSet<string> { "Report::ServiceInfo" };
        var results = new[]
        {
            new ApiTestRunResult { EndpointKey = "Report::ServiceInfo", Passed = false },
            new ApiTestRunResult { EndpointKey = "Report::Health", Passed = true }
        };

        ApiHealthExecutionRunManager.SuiteChecksPassed(results, informational).Should().BeTrue();
    }
}
