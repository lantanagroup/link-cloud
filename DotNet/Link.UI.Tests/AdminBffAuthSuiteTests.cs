using System.Net;
using System.Text;
using Automation.UI.Services.ApiHealth.TestSuites;
using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Xunit;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.AdminBffAuthSteps;

namespace Link.UI.Tests;

public class AdminBffAuthSuiteTests
{
    private static readonly string[] NegativeSteps =
    [
        StepNames.EmptyBearerGet401,
        StepNames.MalformedBearerGet401,
        StepNames.MissingAuthHeaderGet401,
        StepNames.InvalidAuthSchemeGet401
    ];

    [Fact]
    public async Task Negative_auth_steps_skip_when_admin_bff_allows_anonymous_access()
    {
        var calls = 0;
        var suite = CreateSuite(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain")
            };
        });

        var results = await suite.ExecuteAsync(CancellationToken.None);

        calls.Should().Be(1);
        var negatives = results.Where(result => NegativeSteps.Contains(result.EndpointName)).ToList();
        negatives.Should().HaveCount(4);
        negatives.Should().OnlyContain(result =>
            result.Skipped
            && result.SkipReason == AdminBffAuthTestSuite.AnonymousAccessSkipReason
            && result.Passed == false);
    }

    [Fact]
    public async Task Negative_auth_steps_run_when_admin_bff_requires_a_token()
    {
        var calls = 0;
        var suite = CreateSuite(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized", Encoding.UTF8, "text/plain")
            };
        });

        var results = await suite.ExecuteAsync(CancellationToken.None);

        calls.Should().Be(5);
        var negatives = results.Where(result => NegativeSteps.Contains(result.EndpointName)).ToList();
        negatives.Should().HaveCount(4);
        negatives.Should().OnlyContain(result => result.Skipped == false && result.Passed && result.ActualStatusCode == 401);
    }

    private static AdminBffAuthTestSuite CreateSuite(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var registry = Options.Create(new ServiceRegistry
        {
            AdminBffServiceUrl = "http://bff.local"
        });
        var tokens = Options.Create(new LinkTokenServiceSettings());
        return new AdminBffAuthTestSuite(
            new StubFactory(new StubHandler(responder)),
            registry,
            tokens,
            new UnusedTokenService());
    }

    private sealed class UnusedTokenService : ICreateSystemToken
    {
        public Task<string> ExecuteAsync(string key, int timespan) =>
            throw new InvalidOperationException("Token generation is not configured for this test.");
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
