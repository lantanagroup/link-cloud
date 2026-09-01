using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    /// <summary>
    /// The client's job is a token, two calls and one combined answer, so these drive it through a
    /// stub handler and assert on what it asked for as much as on what it returned.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class DmrpApiClientTests
    {
        private const string FacilityId = "100";

        private static DmrpSettings Settings() => new()
        {
            Enabled = true,
            Api = new DmrpApiSettings
            {
                BaseUrl = "https://dmrp.example/",
                TokenUrl = "https://dmrp.example/oauth2/token",
                ClientId = "link",
                ClientSecret = "secret",
                TokenExpiryMarginSeconds = 60
            }
        };

        /// <summary>Answers whatever the test queued, and remembers what it was asked.</summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public List<HttpRequestMessage> Requests { get; } = [];

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(_respond(request));
            }
        }

        private sealed class StubFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;

            public StubFactory(HttpMessageHandler handler) => _handler = handler;

            public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        private static HttpResponseMessage TokenResponse(int expiresIn = 3600) =>
            Json($$"""{"access_token":"token-{{expiresIn}}","expires_in":{{expiresIn}},"token_type":"Bearer"}""");

        private static HttpResponseMessage PlanResponse(params string[] measures)
        {
            var items = measures.Select(m =>
                $$"""{"name":"{{m}}","nhsnorgid":"100","month":5,"year":2026,"reporting":"Y"}""");

            return Json($$"""{"orgid":100,"year":2026,"month":5,"plans":[{{string.Join(",", items)}}]}""");
        }

        private static (DmrpApiClient Client, StubHandler Handler, FakeTimeProvider Clock) CreateClient(
            Func<HttpRequestMessage, HttpResponseMessage> respond, DmrpSettings? settings = null)
        {
            var handler = new StubHandler(respond);
            var factory = new StubFactory(handler);
            var options = Options.Create(settings ?? Settings());
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

            var tokens = new DmrpApiTokenProvider(factory, options,
                NullLogger<DmrpApiTokenProvider>.Instance, clock);

            return (new DmrpApiClient(factory, tokens, options, NullLogger<DmrpApiClient>.Instance), handler, clock);
        }

        private static HttpResponseMessage RouteByPath(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("token", StringComparison.Ordinal))
            {
                return TokenResponse();
            }

            return path.Contains("ps/annual/mrp", StringComparison.Ordinal)
                ? PlanResponse("HAI")
                : PlanResponse("HOB", "CAUTI");
        }

        [Fact]
        public async Task GetReportingPlanAsync_CombinesBothOperationsAndTagsTheComponent()
        {
            var (client, _, _) = CreateClient(RouteByPath);

            var entries = await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            // The component is not in the payload - it is knowable only from which operation
            // answered - so the client stamps it on.
            Assert.Equal(3, entries.Count);
            Assert.Equal(["HOB", "CAUTI"],
                entries.Where(e => e.Component == ReportingComponents.Msc).Select(e => e.Measure));
            Assert.Equal("HAI", Assert.Single(entries, e => e.Component == ReportingComponents.Ps).Measure);
        }

        [Fact]
        public async Task GetReportingPlanAsync_AsksBothOperationsForTheRequestedPeriod()
        {
            var (client, handler, _) = CreateClient(RouteByPath);

            await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            var plans = handler.Requests
                .Where(r => !r.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal))
                .Select(r => r.RequestUri!.PathAndQuery)
                .ToList();

            Assert.Equal(2, plans.Count);
            Assert.Contains(plans, p => p.Contains("/msc?") && p.Contains("nhsnorgid=100")
                                        && p.Contains("year=2026") && p.Contains("month=5"));
            Assert.Contains(plans, p => p.Contains("/ps/annual/mrp?") && p.Contains("nhsnorgid=100"));
        }

        [Fact]
        public async Task GetReportingPlanAsync_CarriesTheBearerTokenOnBothOperations()
        {
            var (client, handler, _) = CreateClient(RouteByPath);

            await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            var plans = handler.Requests
                .Where(r => !r.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal))
                .ToList();

            Assert.All(plans, r =>
            {
                Assert.Equal("Bearer", r.Headers.Authorization?.Scheme);
                Assert.Equal("token-3600", r.Headers.Authorization?.Parameter);
            });
        }

        [Fact]
        public async Task GetReportingPlanAsync_FetchesOneTokenForBothOperations()
        {
            var (client, handler, _) = CreateClient(RouteByPath);

            await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            // Two calls, one token: a token lasts an hour, so minting one per call would mint two
            // to do a single facility's work.
            Assert.Single(handler.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal));
        }

        [Fact]
        public async Task GetReportingPlanAsync_ReusesTheTokenAcrossCallsUntilItNearsExpiry()
        {
            var (client, handler, clock) = CreateClient(RouteByPath);

            await client.GetReportingPlanAsync(FacilityId, 5, 2026);
            await client.GetReportingPlanAsync(FacilityId, 6, 2026);

            Assert.Single(handler.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal));

            // Past the hour, less the margin.
            clock.Advance(TimeSpan.FromMinutes(60));

            await client.GetReportingPlanAsync(FacilityId, 7, 2026);

            Assert.Equal(2, handler.Requests
                .Count(r => r.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task GetReportingPlanAsync_PrefersTheEntrysOwnPeriodOverTheOneAskedFor()
        {
            // A response answering for a different period than the request is reporting something.
            // Relabelling it to the requested period would hide that.
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : Json("""{"plans":[{"name":"HOB","month":4,"year":2025,"reporting":"Y"}]}"""));

            var entries = await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            var entry = entries.First();
            Assert.Equal(4, entry.ReportingMonth);
            Assert.Equal(2025, entry.ReportingYear);
        }

        [Fact]
        public async Task GetReportingPlanAsync_FallsBackToTheRequestedPeriodWhenAnEntryOmitsIt()
        {
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : Json("""{"plans":[{"name":"HOB","reporting":"Y"}]}"""));

            var entries = await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            var entry = entries.First();
            Assert.Equal(5, entry.ReportingMonth);
            Assert.Equal(2026, entry.ReportingYear);
        }

        [Fact]
        public async Task GetReportingPlanAsync_SkipsAnEntryWithNoMeasureName()
        {
            // Nothing identifies what the facility is enrolled in, so there is nothing to record
            // and nothing an admin could map later.
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : Json("""{"plans":[{"name":"","month":5,"year":2026},{"name":"HOB","month":5,"year":2026}]}"""));

            var entries = await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            Assert.All(entries, e => Assert.Equal("HOB", e.Measure));
        }

        [Fact]
        public async Task GetReportingPlanAsync_AFacilityEnrolledInNothing_ReturnsEmpty()
        {
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : Json("""{"plans":[]}"""));

            var entries = await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            // DMRP has no negative representation, so an empty plan is the answer rather than an
            // absence of one.
            Assert.Empty(entries);
        }

        [Fact]
        public async Task GetReportingPlanAsync_WhenAnOperationFails_Throws()
        {
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var ex = await Assert.ThrowsAsync<DmrpApiException>(
                () => client.GetReportingPlanAsync(FacilityId, 5, 2026));

            Assert.Contains("503", ex.Message);
        }

        [Fact]
        public async Task GetReportingPlanAsync_WhenTheTokenIsRefused_Throws()
        {
            var (client, _, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

            var ex = await Assert.ThrowsAsync<DmrpApiException>(
                () => client.GetReportingPlanAsync(FacilityId, 5, 2026));

            Assert.Contains("401", ex.Message);
        }

        [Fact]
        public async Task GetReportingPlanAsync_WhenTheApiIsNotConfigured_SaysSo()
        {
            var settings = Settings();
            settings.Api.BaseUrl = null;

            var (client, handler, _) = CreateClient(RouteByPath, settings);

            var ex = await Assert.ThrowsAsync<DmrpApiException>(
                () => client.GetReportingPlanAsync(FacilityId, 5, 2026));

            // Named rather than surfacing as a null reference somewhere further in, and nothing is
            // sent - an unconfigured environment does not call out at all.
            Assert.Contains("DMRP:Api", ex.Message);
            Assert.Empty(handler.Requests);
        }
    }
}
