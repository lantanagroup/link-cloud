using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Middleware;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;
using LantanaGroup.Link.MockDmrpApi.Settings;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

using Task = System.Threading.Tasks.Task;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Drives the response-delay middleware in a real pipeline, alongside both controllers.
/// </summary>
/// <remarks>
/// The scoping is the property worth proving over HTTP rather than against a path predicate:
/// if the delay ever reached <c>/mock</c>, turning a five-minute delay off would take five
/// minutes, and if it reached <c>/health</c> the container would be restarted mid-test. Both
/// failures only show up once the middleware is actually in a pipeline.
/// <para>
/// These use the real clock, unlike <see cref="ResponseDelayServiceTests"/>. A fake one races
/// here: <c>SendAsync</c> returns before the request has reached the middleware, so advancing
/// the clock can happen before the timer is registered, and the request then waits forever.
/// The assertions are written to stay robust anyway — a configured delay is checked against a
/// <em>lower</em> bound, which a slow agent can only overshoot, and the never-delayed paths
/// are checked against a five-minute delay, so the margin between pass and fail is enormous.
/// </para>
/// <para>
/// One assertion cannot be written that way. Proving nothing holds a request needs an
/// <em>upper</em> bound, which a slow agent can break, so
/// <see cref="WithNoDelayConfigured_TheContractEndpointAnswersImmediately"/> warms the
/// pipeline first: measured cold it took 219-264ms against a 300ms bound, and warm it takes
/// 3-4ms. Anything new here that measures a first request should do the same.
/// </para>
/// </remarks>
public class ResponseDelayPipelineTests : IAsyncLifetime
{
    /// <summary>Long enough to measure, short enough not to slow the suite.</summary>
    private const int ObservableDelayMs = 400;

    /// <summary>Allows for timer granularity; a slow agent can only overshoot this.</summary>
    private static readonly TimeSpan LowerBound = TimeSpan.FromMilliseconds(300);

    /// <summary>A path that is not delayed answers far inside this, however long the delay.</summary>
    private static readonly TimeSpan NotDelayedBudget = TimeSpan.FromSeconds(10);

    private readonly FakeEntryRepository _repository = new();
    private IHost _host = null!;
    private HttpClient _client = null!;
    private IResponseDelayService _delays = null!;
    private string _token = null!;

    public async Task InitializeAsync()
    {
        var settings = new DmrpApiSettings
        {
            AuthClientId = "delay-test-client",
            AuthClientSecret = "delay-test-client-secret",
            SigningKey = "controller-test-signing-key-long-enough-for-hmac-sha512-which-needs-64",
            Issuer = "link-mock-dmrp-tests",
            Audience = "dmrp-api-tests",
            TokenLifetimeSeconds = 3600
        };

        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<IBaseEntityRepository<ReportingPlanEntryEntity>>(_repository);
                    services.AddSingleton<IOptions<DmrpApiSettings>>(Options.Create(settings));
                    services.AddScoped<IReportingPlanService, ReportingPlanService>();
                    services.AddSingleton<IAuthTokenService, AuthTokenService>();
                    services.AddSingleton<IResponseDelayService, ResponseDelayService>();

                    // The support surface is [Authorize]d, and ASP.NET refuses to serve an
                    // endpoint carrying authorization metadata without the middleware. These
                    // tests are about timing, not auth, so the credential is always supplied.
                    services
                        .AddAuthentication(MockControllerTests.LinkCredentialAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, MockControllerTests.LinkCredentialAuthHandler>(
                            MockControllerTests.LinkCredentialAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization(options =>
                        options.AddPolicy(PolicyNames.IsLinkAdmin, p => p.RequireAuthenticatedUser()));

                    services.AddControllers().AddApplicationPart(typeof(MockController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseContractResponseDelay();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapGet("/health", async context =>
                            await context.Response.WriteAsync("Healthy", context.RequestAborted));
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
        _client.DefaultRequestHeaders.Add(
            MockControllerTests.LinkCredentialAuthHandler.HeaderName, "delay-test-user");
        _delays = _host.Services.GetRequiredService<IResponseDelayService>();

        var issued = _host.Services.GetRequiredService<IAuthTokenService>()
            .Issue("client_credentials", "delay-test-client", "delay-test-client-secret", "dmrp.read");
        _token = issued.AccessToken!;

        _repository.Seed(new ReportingPlanEntryEntity
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = "FAC001",
            Component = ReportingComponents.Msc,
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y",
            CreateDate = DateTime.UtcNow
        });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private Task<HttpResponseMessage> GetPlanAsync()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, "/msc?nhsnorgid=FAC001&year=2026&month=5");
        request.Headers.Add("Authorization", $"Bearer {_token}");
        return _client.SendAsync(request);
    }

    private static async Task<TimeSpan> ElapsedAsync(Func<Task<HttpResponseMessage>> call)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await call().WaitAsync(NotDelayedBudget);
        var elapsed = Stopwatch.GetElapsedTime(started);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return elapsed;
    }

    [Fact]
    public async Task WithNoDelayConfigured_TheContractEndpointAnswersImmediately()
    {
        // Warm the pipeline, unmeasured. This is the only assertion in the file with an upper
        // bound, so it is the only one a slow agent can break, and the first request through a
        // freshly built host pays for JIT, routing and the auth handler before any of this
        // service's own code runs. Measured cold that came to 219-264ms against a 300ms
        // bound -- passing, but on a margin no CI agent should be asked to hold.
        (await GetPlanAsync()).StatusCode.Should().Be(HttpStatusCode.OK);

        var elapsed = await ElapsedAsync(GetPlanAsync);

        elapsed.Should().BeLessThan(LowerBound, "nothing should be holding the request");
    }

    [Fact]
    public async Task AConfiguredDelay_HoldsTheContractEndpoint()
    {
        _delays.Set(ObservableDelayMs);

        var elapsed = await ElapsedAsync(GetPlanAsync);

        // A lower bound: a loaded agent can only make this longer, never shorter.
        elapsed.Should().BeGreaterThanOrEqualTo(LowerBound);
    }

    [Fact]
    public async Task ALongDelay_NeverReachesTheEndpointThatTurnsItOff()
    {
        // The escape hatch. Were /mock delayed too, clearing a five-minute delay would take
        // five minutes and the only way out would be a restart. The delay set here is that
        // five minutes, so the margin between passing and failing is enormous.
        _delays.Set(ResponseDelay.MaxMilliseconds);

        await ElapsedAsync(() => _client.GetAsync("/api/mock-dmrp/delay"));

        var cleared = await _client.DeleteAsync("/api/mock-dmrp/delay").WaitAsync(NotDelayedBudget);
        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _delays.Current.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ALongDelay_NeverReachesTheRestOfTheSupportSurface()
    {
        // Seeding has to stay usable while a delay is configured, or a test could not set up
        // the scenario it wants to observe timing out.
        _delays.Set(ResponseDelay.MaxMilliseconds);

        await ElapsedAsync(() => _client.GetAsync("/api/mock-dmrp/entries/search"));
    }

    [Fact]
    public async Task ALongDelay_NeverReachesHealth()
    {
        // Otherwise the container misses its probe timeout and is restarted, which reads as
        // an outage rather than a test in progress -- and takes the delay with it.
        _delays.Set(ResponseDelay.MaxMilliseconds);

        await ElapsedAsync(() => _client.GetAsync("/health"));
    }

    [Fact]
    public async Task ClearingTheDelay_RestoresNormalResponseTime()
    {
        _delays.Set(ResponseDelay.MaxMilliseconds);
        await _client.DeleteAsync("/api/mock-dmrp/delay");

        var elapsed = await ElapsedAsync(GetPlanAsync);

        elapsed.Should().BeLessThan(LowerBound);
    }

    [Fact]
    public async Task ConcurrentCallers_WaitOnceRatherThanInTurn()
    {
        // Nothing serialises on the delay. Ten callers waiting in turn would take ten times
        // the delay; waiting concurrently takes roughly one.
        _delays.Set(ObservableDelayMs);

        var started = Stopwatch.GetTimestamp();
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => GetPlanAsync()))
            .WaitAsync(NotDelayedBudget);
        var elapsed = Stopwatch.GetElapsedTime(started);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        elapsed.Should().BeGreaterThanOrEqualTo(LowerBound, "each still waited");
        elapsed.Should().BeLessThan(
            TimeSpan.FromMilliseconds(ObservableDelayMs * 5),
            "ten callers waiting in turn would take ten times the delay");
    }

    [Fact]
    public async Task ACallerThatGivesUpMidDelay_DoesNotHoldTheRequest()
    {
        _delays.Set(ResponseDelay.MaxMilliseconds);

        using var cts = new CancellationTokenSource();
        var request = new HttpRequestMessage(
            HttpMethod.Get, "/msc?nhsnorgid=FAC001&year=2026&month=5");
        request.Headers.Add("Authorization", $"Bearer {_token}");

        var pending = _client.SendAsync(request, cts.Token);
        await cts.CancelAsync();

        var act = async () => await pending.WaitAsync(NotDelayedBudget);
        await act.Should().ThrowAsync<OperationCanceledException>();

        // The service is still serving everyone else.
        _delays.Clear();
        await ElapsedAsync(GetPlanAsync);
    }

    // ------------------------------------------------------- the delay endpoints

    [Fact]
    public async Task GetDelay_ReportsNoDelayByDefault()
    {
        var response = await _client.GetAsync("/api/mock-dmrp/delay");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var model = await response.Content.ReadFromJsonAsync<MockDelayModel>();
        model!.IsActive.Should().BeFalse();
        model.Milliseconds.Should().Be(0);
        model.ConfiguredOn.Should().BeNull();
        model.AppliesTo.Should().NotBeNullOrWhiteSpace("the scoping is the surprising part");
    }

    [Fact]
    public async Task SetDelay_Returns200WithTheStateNowInForce()
    {
        var response = await _client.PutAsJsonAsync("/api/mock-dmrp/delay", new { milliseconds = 4_000 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var model = await response.Content.ReadFromJsonAsync<MockDelayModel>();
        model!.Milliseconds.Should().Be(4_000);
        model.IsActive.Should().BeTrue();
        model.ConfiguredOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var read = await (await _client.GetAsync("/api/mock-dmrp/delay")).Content.ReadFromJsonAsync<MockDelayModel>();
        read!.Milliseconds.Should().Be(4_000);
    }

    [Fact]
    public async Task SetDelayToZero_TurnsItOff()
    {
        await _client.PutAsJsonAsync("/api/mock-dmrp/delay", new { milliseconds = 4_000 });

        var response = await _client.PutAsJsonAsync("/api/mock-dmrp/delay", new { milliseconds = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var model = await response.Content.ReadFromJsonAsync<MockDelayModel>();
        model!.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ResponseDelay.MaxMilliseconds + 1)]
    public async Task SetDelay_OutsideTheAllowedRange_Returns400(int milliseconds)
    {
        var response = await _client.PutAsJsonAsync("/api/mock-dmrp/delay", new { milliseconds });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _delays.Current.IsActive.Should().BeFalse("a rejected value must not take effect");
    }

    [Fact]
    public async Task ClearDelay_IsIdempotent()
    {
        _delays.Set(5_000);

        (await _client.DeleteAsync("/api/mock-dmrp/delay")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync("/api/mock-dmrp/delay")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TheDelayRouteIsNotReadAsAnEntryIdentifier()
    {
        // /api/mock-dmrp/entries/{id} would otherwise swallow it and answer "Invalid Id format".
        var response = await _client.GetAsync("/api/mock-dmrp/delay");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Invalid Id format");
    }
}
