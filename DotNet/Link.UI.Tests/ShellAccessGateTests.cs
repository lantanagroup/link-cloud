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
    [InlineData("/hubs/link")]
    [InlineData("/hubs/runs")]
    [InlineData("/hubs/cleanup")]
    [InlineData("/Home/overview")]
    [InlineData("/Home/overview/data")]
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
    }

    [Theory]
    [InlineData("/hubs/link")]
    [InlineData("/hubs/runs")]
    [InlineData("/hubs/cleanup")]
    public void Require_session_signs_in_before_a_hub_connects(string path)
    {
        ShellAccessGate.Evaluate(
                false,
                true,
                path,
                new AdminBffUser { IsAuthenticated = false },
                out _)
            .Should().Be(ShellAccessGate.Decision.RedirectToLogin);

        ShellAccessGate.Evaluate(
                false,
                true,
                path,
                new AdminBffUser { IsAuthenticated = true, Email = "ada@example.com" },
                out _)
            .Should().Be(ShellAccessGate.Decision.Continue);
    }

    [Theory]
    [InlineData("/Home/overview")]
    [InlineData("/Home/overview/data")]
    public void Overview_requires_the_same_sign_in_as_the_shell(string path)
    {
        ShellAccessGate.Evaluate(false, false, path, user: null, out var blocked)
            .Should().Be(ShellAccessGate.Decision.Unavailable);
        blocked.Should().Be(ShellAccessGate.AnonymousBlockedMessage);

        ShellAccessGate.Evaluate(
                false,
                true,
                path,
                new AdminBffUser { IsAuthenticated = false },
                out _)
            .Should().Be(ShellAccessGate.Decision.RedirectToLogin);

        ShellAccessGate.Evaluate(
                false,
                true,
                path,
                new AdminBffUser { IsAuthenticated = true, Email = "ada@example.com" },
                out _)
            .Should().Be(ShellAccessGate.Decision.Continue);

        ShellAccessGate.Evaluate(true, false, path, user: null, out var open)
            .Should().Be(ShellAccessGate.Decision.Continue);
        open.Should().BeNull();
    }

    [Fact]
    public void Anonymous_on_leaves_the_hubs_open()
    {
        ShellAccessGate.Evaluate(true, false, "/hubs/runs", user: null, out var message)
            .Should().Be(ShellAccessGate.Decision.Continue);
        message.Should().BeNull();
    }

    [Fact]
    public void Anonymous_on_without_session_requirement_leaves_the_shell_open()
    {
        ShellAccessGate.Evaluate(true, false, "/Tenants", user: null, out var message)
            .Should().Be(ShellAccessGate.Decision.Continue);
        message.Should().BeNull();
    }

    [Theory]
    [InlineData("/api/runs")]
    [InlineData("/api/runs/metrics")]
    [InlineData("/api/runs/start")]
    [InlineData("/api/api-health-runs/start-all")]
    [InlineData("/api/api-health-runs/start-all-for-pipeline")]
    [InlineData("/api/api-health-runs/22222222-2222-2222-2222-222222222222/status")]
    [InlineData("/api/api-health-runs/22222222-2222-2222-2222-222222222222/results")]
    public void Anonymous_off_closes_native_automation_apis_when_bearer_is_disabled(string path)
    {
        ShellAccessGate.IsClosedNativeApi(false, false, path).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/runs/metrics")]
    [InlineData("/api/api-health-runs/start-all-for-pipeline")]
    public void Bearer_enabled_leaves_bearer_routes_to_the_auth_middleware(string path)
    {
        ShellAccessGate.IsClosedNativeApi(false, true, path).Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/api-health-runs/start-all")]
    [InlineData("/api/api-health-runs/22222222-2222-2222-2222-222222222222/status")]
    [InlineData("/api/api-health-runs/22222222-2222-2222-2222-222222222222/results")]
    public void Bearer_enabled_still_closes_routes_that_are_not_bearer_protected(string path)
    {
        ShellAccessGate.IsClosedNativeApi(false, true, path).Should().BeTrue();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/login")]
    [InlineData("/api/user")]
    [InlineData("/Tenants")]
    [InlineData("/hubs/runs")]
    public void The_closure_does_not_apply_outside_the_native_automation_api(string path)
    {
        ShellAccessGate.IsClosedNativeApi(false, false, path).Should().BeFalse();
    }

    [Fact]
    public void Anonymous_on_leaves_the_native_automation_api_open()
    {
        ShellAccessGate.IsClosedNativeApi(true, false, "/api/runs/metrics").Should().BeFalse();
        ShellAccessGate.IsClosedNativeApi(true, false, "/api/api-health-runs/start-all").Should().BeFalse();
    }
}

