using System.Net;
using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Link.UI.Tests;

public class SignInRulesTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Sign_in_is_required_only_when_the_session_flag_is_on(bool requireBffSession, bool required)
    {
        SignInRules.IsRequired(requireBffSession).Should().Be(required);
    }

    [Theory]
    [InlineData("/Tenants", "/Tenants")]
    [InlineData("/Tenants?q=a", "/Tenants?q=a")]
    [InlineData("/dashboard", "/")]
    [InlineData("/", "/")]
    [InlineData("/home/index", "/")]
    [InlineData("/Auth/Login", null)]
    [InlineData("/Auth/Logout", null)]
    [InlineData("/logout", null)]
    [InlineData("/api/login", null)]
    [InlineData("/api/logout", null)]
    [InlineData("https://evil.example/phish", null)]
    [InlineData("//evil.example", null)]
    [InlineData("/\\evil", null)]
    public void Return_target_is_local_and_skips_auth_loops(string candidate, string? expected)
    {
        SignInRules.NormalizeReturn(candidate).Should().Be(expected);
    }

    [Fact]
    public void Query_return_wins_over_referer_and_the_current_page()
    {
        var context = Context("/Auth/Login", "?returnUrl=/Reports");
        context.Request.Headers.Referer = "http://localhost/Logs";

        SignInRules.ChooseReturn(context.Request, includeCurrent: true).Should().Be("/Reports");
    }

    [Fact]
    public void Same_origin_referer_is_used_when_the_query_is_absent()
    {
        var context = Context("/Auth/Login", "");
        context.Request.Headers.Referer = "http://localhost/Logs?page=2";

        SignInRules.ChooseReturn(context.Request, includeCurrent: false).Should().Be("/Logs?page=2");
    }

    [Fact]
    public void A_foreign_referer_is_ignored()
    {
        var context = Context("/Tenants", "");
        context.Request.Headers.Referer = "https://evil.example/Tenants";

        SignInRules.ChooseReturn(context.Request, includeCurrent: true).Should().Be("/Tenants");
        SignInRules.ChooseReturn(context.Request, includeCurrent: false).Should().BeNull();
    }

    [Fact]
    public void Taking_the_return_cookie_clears_it()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Headers.Cookie = SignInRules.ReturnCookieName + "=/Tenants";

        SignInRules.TakeReturn(context.Request, context.Response).Should().Be("/Tenants");
        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain(SignInRules.ReturnCookieName);
        setCookie.Should().NotContain("/Tenants");

        var again = new DefaultHttpContext();
        again.Request.Scheme = "http";
        again.Request.Host = new HostString("localhost");
        SignInRules.TakeReturn(again.Request, again.Response).Should().BeNull();
    }

    [Fact]
    public void A_rejected_cookie_is_still_cleared()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Headers.Cookie = SignInRules.ReturnCookieName + "=https://evil.example/phish";

        SignInRules.TakeReturn(context.Request, context.Response).Should().BeNull();
        context.Response.Headers.SetCookie.ToString().Should().Contain(SignInRules.ReturnCookieName);
    }

    private static DefaultHttpContext Context(string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }
}

public class SignInFlowTests
{
    [Fact]
    public async Task Login_stays_on_this_host_when_sign_in_is_not_required()
    {
        await using var factory = new AnonymousHost();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await home.Content.ReadAsStringAsync();
        html.Should().NotContain("bi-box-arrow-in-right");
        html.Should().NotContain("/Auth/Login");

        var login = await client.GetAsync("/Auth/Login?returnUrl=/Reports");
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        login.Headers.Location!.OriginalString.Should().Be("/Reports");
        login.Headers.Location.OriginalString.Should().NotBe("/api/login");

        var plain = await client.GetAsync("/Auth/Login");
        plain.Headers.Location!.OriginalString.Should().Be("/");

        var logout = await client.GetAsync("/Auth/Logout");
        logout.StatusCode.Should().Be(HttpStatusCode.Redirect);
        logout.Headers.Location!.OriginalString.Should().Be("/");
        logout.Headers.Location.OriginalString.Should().NotBe("/api/logout");
    }

    [Fact]
    public async Task Login_challenges_and_restores_a_local_return_when_sign_in_is_required()
    {
        await using var factory = new SessionHost();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await client.GetAsync("/Auth/Login?returnUrl=" + Uri.EscapeDataString("/Reports"));
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        login.Headers.Location!.OriginalString.Should().Be("/api/login");
        login.Headers.GetValues("Set-Cookie").Should().Contain(value => value.Contains("%2FReports", StringComparison.Ordinal));

        var rejected = await client.GetAsync("/Auth/Login?returnUrl=https://evil.example/phish");
        rejected.Headers.Location!.OriginalString.Should().Be("/api/login");
        var rejectedCookie = string.Join("\n", rejected.Headers.GetValues("Set-Cookie"));
        rejectedCookie.Should().NotContain("evil.example");

        var shell = await client.GetAsync("/Tenants?includeDeleted=false");
        shell.StatusCode.Should().Be(HttpStatusCode.Redirect);
        shell.Headers.Location!.OriginalString.Should().Be("/api/login");
        shell.Headers.GetValues("Set-Cookie").Should().Contain(value =>
            value.Contains("%2FTenants%3FincludeDeleted%3Dfalse", StringComparison.Ordinal));

        using var back = new HttpRequestMessage(HttpMethod.Get, "/dashboard");
        back.Headers.Add("Cookie", "test-signed-in=1; " + SignInRules.ReturnCookieName + "=/Tenants?includeDeleted=false");
        var landing = await client.SendAsync(back);
        landing.StatusCode.Should().Be(HttpStatusCode.Redirect);
        landing.Headers.Location!.OriginalString.Should().Be("/Tenants?includeDeleted=false");
        landing.Headers.GetValues("Set-Cookie").Should().Contain(value =>
            value.StartsWith(SignInRules.ReturnCookieName + "=", StringComparison.Ordinal)
            && !value.Contains("/Tenants", StringComparison.Ordinal));

        var again = await client.GetAsync("/dashboard");
        again.StatusCode.Should().Be(HttpStatusCode.Redirect);
        again.Headers.Location!.OriginalString.Should().NotContain("Tenants");
    }

    [Fact]
    public void Views_and_scripts_do_not_hard_code_the_bff_auth_paths()
    {
        var root = ContentRoot();
        var hits = Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "wwwroot"), "*.js", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("/api/login", StringComparison.Ordinal)
                    || row.line.Contains("/api/logout", StringComparison.Ordinal))
                .Select(row => Path.GetRelativePath(root, row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Only_sign_in_rules_name_the_bff_auth_paths()
    {
        var root = ContentRoot();
        var hits = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.EndsWith("SignInRules.cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("/api/login", StringComparison.Ordinal)
                    || row.line.Contains("/api/logout", StringComparison.Ordinal))
                .Select(row => Path.GetRelativePath(root, row.file) + ":" + (row.index + 1) + " " + row.line.Trim()))
            .ToList();

        hits.Should().BeEmpty();
    }

    private static string ContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var nested = Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj");
            if (File.Exists(nested))
                return Path.Combine(dir.FullName, "DotNet", "Link.UI");
            var sibling = Path.Combine(dir.FullName, "Link.UI", "Link.UI.csproj");
            if (File.Exists(sibling))
                return Path.Combine(dir.FullName, "Link.UI");
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Link.UI content root was not found.");
    }

    private sealed class AnonymousHost : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(WebHostDefaults.ContentRootKey, ContentRoot());
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDB:ConnectionString"] = "not-a-connection-string",
                    ["MongoDB:DatabaseName"] = "link-ui-sign-in-anonymous",
                    ["LinkUi:AutomationEnabled"] = "false",
                    ["Authentication:EnableAnonymousAccess"] = "true",
                    ["Authentication:RequireBffSession"] = "false",
                    ["Authentication:ApiBearer:Enabled"] = "false",
                    ["ExternalConfigurationSource"] = "",
                    ["Telemetry:EnableTelemetry"] = "false"
                });
            });
        }
    }

    private sealed class SessionHost : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(WebHostDefaults.ContentRootKey, ContentRoot());
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDB:ConnectionString"] = "not-a-connection-string",
                    ["MongoDB:DatabaseName"] = "link-ui-sign-in-session",
                    ["LinkUi:AutomationEnabled"] = "false",
                    ["Authentication:EnableAnonymousAccess"] = "true",
                    ["Authentication:RequireBffSession"] = "true",
                    ["Authentication:ApiBearer:Enabled"] = "false",
                    ["ExternalConfigurationSource"] = "",
                    ["Telemetry:EnableTelemetry"] = "false"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAdminBffUserService>();
                services.AddSingleton<IAdminBffUserService, HeaderUser>();
            });
        }
    }

    private sealed class HeaderUser : IAdminBffUserService
    {
        private readonly IHttpContextAccessor _accessor;

        public HeaderUser(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public Task<AdminBffUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var cookie = _accessor.HttpContext?.Request.Headers.Cookie.ToString() ?? "";
            var signedIn = cookie.Contains("test-signed-in=1", StringComparison.Ordinal);
            return Task.FromResult<AdminBffUser?>(new AdminBffUser
            {
                IsAuthenticated = signedIn,
                Email = signedIn ? "ada@example.com" : null
            });
        }
    }
}
