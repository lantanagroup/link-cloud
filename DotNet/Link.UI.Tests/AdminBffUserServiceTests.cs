using System.Net;
using System.Text;
using FluentAssertions;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Link.UI.Tests;

public class AdminBffUserServiceTests
{
    [Fact]
    public async Task Forwards_the_browser_cookie_and_reads_the_user()
    {
        string? cookie = null;
        var sut = CreateSut(
            request =>
            {
                cookie = request.Headers.TryGetValues("Cookie", out var values) ? values.Single() : null;
                return Json(HttpStatusCode.OK, """
                    {"firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","roles":["LinkUser"],"permissions":[]}
                    """);
            },
            out var httpContext);
        httpContext.Request.Headers.Cookie = "link_cookie=session";

        var user = await sut.GetCurrentUserAsync();

        cookie.Should().Be("link_cookie=session");
        user!.IsAuthenticated.Should().BeTrue();
        user.UserName.Should().Be("Ada Lovelace");
        user.Email.Should().Be("ada@example.com");
        user.Roles.Should().ContainSingle().Which.Should().Be("LinkUser");
    }

    [Fact]
    public async Task Unauthorized_is_signed_out()
    {
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), out _);

        var user = await sut.GetCurrentUserAsync();

        user!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_success_payload_is_not_a_signed_in_user()
    {
        var sut = CreateSut(
            _ => Json(HttpStatusCode.OK, """
                {"firstName":"","lastName":"","email":"","roles":[],"permissions":[]}
                """),
            out _);

        var user = await sut.GetCurrentUserAsync();

        user!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Transport_failure_returns_null()
    {
        var sut = CreateSut(_ => throw new HttpRequestException("connection refused"), out _);

        var user = await sut.GetCurrentUserAsync();

        user.Should().BeNull();
    }

    private static AdminBffUserService CreateSut(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        out DefaultHttpContext httpContext)
    {
        httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var client = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("http://bff.local/")
        };
        return new AdminBffUserService(client, accessor, NullLogger<AdminBffUserService>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
