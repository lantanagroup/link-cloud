using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class BackLinkRulesTests
{
    [Theory]
    [InlineData("/Logs/Acquisition", "/Logs", null, false)]
    [InlineData("/Logs/Sftp", "/Logs", null, false)]
    [InlineData("/Logs/Audit", "/Logs", null, false)]
    [InlineData("/Logs/Kafka", "/Logs", null, false)]
    [InlineData("/Logs", "/Logs", null, false)]
    [InlineData("/Configuration/Vendors", "/Configuration", null, false)]
    [InlineData("/System/Health", "/System", null, false)]
    [InlineData("/Configuration/Versions", "/Configuration/Vendors", null, true)]
    [InlineData("/Logs/Acquisition/detail", "/Logs/Acquisition", null, true)]
    [InlineData("/Automation/Runs/11111111-1111-4111-8111-000000000001", "/Automation", null, true)]
    [InlineData("/Logs/Acquisition", "/Logs", "/Tenants", true)]
    [InlineData("/Configuration/Vendors", "/Configuration/Vendors", null, false)]
    public void Back_hides_a_self_or_same_section_target(string current, string fallback, string? origin, bool show)
    {
        BackLinkRules.Show(current, fallback, origin).Should().Be(show);
    }

    [Fact]
    public void Labels_use_one_back_to_form()
    {
        BackLinkRules.LabelFor(null, "/Configuration/Validation", "Validation").Should().Be("Back to validation");
        BackLinkRules.LabelFor(null, "/Configuration/Vendors", "Vendors").Should().Be("Back to vendors");
        BackLinkRules.LabelFor(null, "/Configuration/Notifications", "Notifications").Should().Be("Back to notifications");
        BackLinkRules.LabelFor(null, "/Configuration/Terminology", "Terminology").Should().Be("Back to terminology");
        BackLinkRules.LabelFor(null, "/System/Users", "Accounts").Should().Be("Back to users");
        BackLinkRules.LabelFor(null, "/Metrics", "All use cases").Should().Be("Back to metrics");
        BackLinkRules.LabelFor(null, "/Logs/Sftp", "SFTP logs").Should().Be("Back to logs");
        BackLinkRules.LabelFor("/Tenants", "/Logs", "Back to logs").Should().Be("Back to tenants");
    }
}
