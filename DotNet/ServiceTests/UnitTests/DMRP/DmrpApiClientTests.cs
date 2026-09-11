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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetReportingPlanAsync_WhenTheApiTimesOut_ReportsItAsAnApiFailure(bool onTheTokenCall)
        {
            // Both legs have their own guard, and the token is fetched first -- so a stub that throws
            // for every request would only ever prove the token provider's, leaving the client's
            // untested and a plan-call timeout still escaping as a 500.
            var (client, _, _) = CreateClient(request =>
            {
                var isToken = request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal);

                if (isToken == onTheTokenCall)
                {
                    throw new TaskCanceledException(
                        "The request was canceled due to the configured HttpClient.Timeout.",
                        new TimeoutException());
                }

                return RouteByPath(request);
            });

            // HttpClient reports its own timeout as a TaskCanceledException, which is an
            // OperationCanceledException -- so a filter excluding those lets it past and the request
            // ends as an unhandled 500 rather than the 502 the endpoint documents. A timeout is the
            // most likely way a third party fails, so it must land with the other transport faults.
            await Assert.ThrowsAsync<DmrpApiException>(
                () => client.GetReportingPlanAsync(FacilityId, 5, 2026));
        }

        [Fact]
        public async Task GetReportingPlanAsync_WhenTheCallerCancels_StaysACancellation()
        {
            var (client, _, _) = CreateClient(_ => throw new TaskCanceledException());

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // The other side of the same distinction: the caller giving up is not an API failure and
            // must keep unwinding as cancellation rather than being dressed up as a bad gateway.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.GetReportingPlanAsync(FacilityId, 5, 2026, cts.Token));
        }

        [Theory]
        [InlineData("<html><body>Sign in to continue</body></html>", "text/html")]
        [InlineData("{\"orgid\":100,\"plans\":[{\"name\":\"HOB\"", "application/json")]
        public async Task GetReportingPlanAsync_WhenTheBodyCannotBeRead_ReportsItAsAnApiFailure(
            string body, string contentType)
        {
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body, Encoding.UTF8, contentType)
                    });

            // A 200 is not a promise of JSON: a proxy or captive portal answers HTML with a success
            // code, and a connection cut mid-body leaves truncated JSON. Both used to escape the
            // wrapping as a NotSupportedException or JsonException and surface as a 500.
            await Assert.ThrowsAsync<DmrpApiException>(
                () => client.GetReportingPlanAsync(FacilityId, 5, 2026));
        }

        [Fact]
        public async Task GetReportingPlanAsync_WhenTheApiRefusesTheToken_TheNextCallFetchesAFreshOne()
        {
            var refuse = true;

            var (client, handler, _) = CreateClient(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal))
                {
                    return TokenResponse();
                }

                if (refuse)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                return RouteByPath(request);
            });

            await Assert.ThrowsAsync<DmrpApiException>(
                () => client.GetReportingPlanAsync(FacilityId, 5, 2026));

            refuse = false;
            await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            // A token is cached until its stated expiry, but a revoked or rotated credential stops
            // being accepted long before that. Without discarding it on a 401 the dead token would be
            // replayed for the rest of its lifetime -- an hour of failing refreshes with nothing an
            // operator could do but restart the host.
            var tokenRequests = handler.Requests
                .Count(r => r.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal));

            Assert.Equal(2, tokenRequests);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("[]")]
        public async Task GetReportingPlanAsync_APlansValueThatIsNullOrEmpty_ReadsAsNoEnrollments(string plans)
        {
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : Json($$"""{"orgid":100,"year":2026,"month":5,"plans":{{plans}}}"""));

            // The property initializer is no defence: deserialization writes an explicit null over
            // it, and the count that follows would throw before anything wrapped it -- surfacing as
            // a 500 rather than the 502 the endpoint documents for a bad answer from DMRP.
            Assert.Empty(await client.GetReportingPlanAsync(FacilityId, 5, 2026));
        }

        [Theory]
        [InlineData(0, 2026)]
        [InlineData(13, 2026)]
        [InlineData(5, 1999)]
        [InlineData(5, 2101)]
        public async Task GetReportingPlanAsync_AnEntryInAPeriodLinkCannotRecord_IsSkipped(int month, int year)
        {
            var (client, _, _) = CreateClient(request =>
                request.RequestUri!.AbsolutePath.EndsWith("token", StringComparison.Ordinal)
                    ? TokenResponse()
                    : Json($$"""
                        {"orgid":100,"year":2026,"month":5,"plans":[
                          {"name":"HOB","nhsnorgid":"100","month":{{month}},"year":{{year}},"reporting":"Y"}]}
                        """));

            var entries = await client.GetReportingPlanAsync(FacilityId, 5, 2026);

            // The sync writes through the repository rather than the manager, so nothing downstream
            // applies the bounds the API enforces on the same column. A row stored in month 13 sits
            // outside every look-ahead window and outside the period withdrawal is scoped to, so it
            // could never be matched, shown or cleared again.
            Assert.Empty(entries);
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

            // The count first: Assert.All is vacuously true on an empty collection, so on its own
            // it would pass just as happily if the nameless entry had taken the named one with it.
            // The stub answers both operations with this payload, so the survivor arrives once per
            // component.
            Assert.Equal(2, entries.Count);
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
