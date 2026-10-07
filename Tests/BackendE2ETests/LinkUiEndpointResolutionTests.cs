using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class LinkUiEndpointResolutionTests
{
    [Fact]
    public void Link_base_url_wins_over_the_legacy_variable()
    {
        var resolved = TestConfig.ResolveLinkUiBaseUrl(name => name switch
        {
            "LINK_UI_BASE_URL" => " http://localhost:5280 ",
            "AUTOMATION_UI_BASE_URL" => "http://localhost:5256",
            _ => null
        });

        Assert.Equal("http://localhost:5280", resolved);
    }

    [Fact]
    public void Legacy_base_url_is_used_when_the_link_variable_is_unset()
    {
        var resolved = TestConfig.ResolveLinkUiBaseUrl(name => name switch
        {
            "LINK_UI_BASE_URL" => "  ",
            "AUTOMATION_UI_BASE_URL" => "http://localhost:5280",
            _ => null
        });

        Assert.Equal("http://localhost:5280", resolved);
    }

    [Fact]
    public void Compose_link_ui_port_is_used_when_both_variables_are_unset()
    {
        var resolved = TestConfig.ResolveLinkUiBaseUrl(_ => null);

        Assert.Equal(TestConfig.LocalLinkUiBaseUrl, resolved);
        Assert.Equal("http://localhost:5258", resolved);
    }

    [Fact]
    public void Process_environment_follows_the_same_precedence()
    {
        var expected = TestConfig.ResolveLinkUiBaseUrl(static name => Environment.GetEnvironmentVariable(name));
        Assert.Equal(expected, TestConfig.AutomationUiBase);

        var link = Environment.GetEnvironmentVariable("LINK_UI_BASE_URL");
        var legacy = Environment.GetEnvironmentVariable("AUTOMATION_UI_BASE_URL");
        if (!string.IsNullOrWhiteSpace(link))
            Assert.Equal(link.Trim(), TestConfig.AutomationUiBase);
        else if (!string.IsNullOrWhiteSpace(legacy))
            Assert.Equal(legacy.Trim(), TestConfig.AutomationUiBase);
        else
            Assert.Equal(TestConfig.LocalLinkUiBaseUrl, TestConfig.AutomationUiBase);
    }
}
