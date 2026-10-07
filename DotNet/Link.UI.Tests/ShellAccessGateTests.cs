using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Link.UI.Tests;

public class ShellAccessGateTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/login")]
    [InlineData("/api/user")]
    public void Anonymous_off_keeps_health_and_api_open(string path)
    {
        var decision = ShellAccessGate.Evaluate(false, false, path, user: null, out var message);

        decision.Should().Be(ShellAccessGate.Decision.Continue);
        message.Should().BeNull();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Tenants")]
    [InlineData("/css/site.css")]
    public void Anonymous_off_blocks_the_shell(string path)
    {
        var decision = ShellAccessGate.Evaluate(false, false, path, user: null, out var message);

        decision.Should().Be(ShellAccessGate.Decision.Unavailable);
        message.Should().Be(ShellAccessGate.AnonymousBlockedMessage);
    }

    [Fact]
    public void Require_session_redirects_when_anonymous_access_is_off()
    {
        var decision = ShellAccessGate.Evaluate(
            allowAnonymousAccess: false,
            requireBffSession: true,
            path: "/",
            user: new AdminBffUser { IsAuthenticated = false },
            out var message);

        decision.Should().Be(ShellAccessGate.Decision.RedirectToLogin);
        message.Should().BeNull();
    }

    [Fact]
    public void Require_session_returns_503_when_admin_bff_cannot_be_checked()
    {
        var decision = ShellAccessGate.Evaluate(true, true, "/Tenants", user: null, out var message);

        decision.Should().Be(ShellAccessGate.Decision.Unavailable);
        message.Should().Be(ShellAccessGate.SessionCheckFailedMessage);
    }

    [Fact]
    public void Require_session_allows_a_signed_in_user_and_static_files()
    {
        ShellAccessGate.Evaluate(
                true,
                true,
                "/",
                new AdminBffUser { IsAuthenticated = true, Email = "ada@example.com" },
                out _)
            .Should().Be(ShellAccessGate.Decision.Continue);

        ShellAccessGate.Evaluate(false, true, "/css/site.css", user: null, out _)
            .Should().Be(ShellAccessGate.Decision.Continue);

        ShellAccessGate.Evaluate(false, true, "/logout", user: null, out _)
            .Should().Be(ShellAccessGate.Decision.Continue);

        ShellAccessGate.Evaluate(false, true, "/hubs/link", user: null, out _)
            .Should().Be(ShellAccessGate.Decision.Continue);
    }

    [Fact]
    public void Anonymous_on_without_session_requirement_leaves_the_shell_open()
    {
        ShellAccessGate.Evaluate(true, false, "/Tenants", user: null, out var message)
            .Should().Be(ShellAccessGate.Decision.Continue);
        message.Should().BeNull();
    }
}
