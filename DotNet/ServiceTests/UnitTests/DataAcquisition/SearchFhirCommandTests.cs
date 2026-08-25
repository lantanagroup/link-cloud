using System.Net;
using System.Text;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Telemetry;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class SearchFhirCommandTests
{
    // ── Test seam ─────────────────────────────────────────────────────────────

    private sealed class TestableSearchFhirCommand : SearchFhirCommand
    {
        private readonly HttpMessageHandler _stubHandler;

        public TestableSearchFhirCommand(
            ILogger<SearchFhirCommand> logger,
            IDataAcquisitionServiceMetrics metrics,
            IDistributedSemaphoreProvider semaphoreProvider,
            IOptions<DistributedLockSettings> lockSettings,
            IAuthenticationRetrievalService authService,
            IOptionsMonitor<TelemetrySettings> telemetrySettings,
            HttpMessageHandler stubHandler)
            : base(logger, new HttpClient(), metrics, semaphoreProvider, lockSettings, authService, telemetrySettings)
        {
            _stubHandler = stubHandler;
        }

        protected override HttpMessageHandler CreateInnerHttpMessageHandler() => _stubHandler;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _queue = new();
        private readonly List<HttpRequestMessage> _captured = new();

        public IReadOnlyList<HttpRequestMessage> CapturedRequests => _captured;

        public void Enqueue(HttpResponseMessage response) => _queue.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _captured.Add(request);
            if (_queue.TryDequeue(out var response))
                return Task.FromResult(response);
            throw new InvalidOperationException("StubHttpMessageHandler: no more queued responses.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpResponseMessage OkBundleResponse(Bundle bundle)
    {
        var json = new FhirJsonSerializer().SerializeToString(bundle);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/fhir+json")
        };
    }

    private static HttpResponseMessage TooManyRequestsResponse(int retryAfterSeconds)
    {
        var outcome = new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.Transient,
                    Diagnostics = "Rate limit exceeded"
                }
            ]
        };
        var response = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent(
                new FhirJsonSerializer().SerializeToString(outcome),
                Encoding.UTF8,
                "application/fhir+json")
        };
        response.Headers.Add("Retry-After", retryAfterSeconds.ToString());
        return response;
    }

    private static Bundle BundleWithNoNext(string id) =>
        new() { Id = id, Type = Bundle.BundleType.Searchset };

    private static Bundle BundleWithNextLink(string id, string nextUrl)
    {
        var b = new Bundle { Id = id, Type = Bundle.BundleType.Searchset };
        b.Link.Add(new Bundle.LinkComponent { Relation = "next", Url = nextUrl });
        return b;
    }

    private static HttpResponseMessage InternalServerErrorResponse()
    {
        var outcome = new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.Exception,
                    Diagnostics = "Internal server error"
                }
            ]
        };
        return new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                new FhirJsonSerializer().SerializeToString(outcome),
                Encoding.UTF8,
                "application/fhir+json")
        };
    }

    private static SearchFhirCommandRequest CreateRequest(int maxConcurrentRequests = 1) =>
        new(
            queryConfig: new FhirQueryConfigurationModel
            {
                FhirServerBaseUrl = "http://fhir.test",
                MaxConcurrentRequests = maxConcurrentRequests,
                Authentication = null
            },
            resourceType: ResourceType.Patient,
            searchParams: new SearchParams(),
            facilityId: "test-facility",
            patientId: "test-patient",
            correlationId: "test-correlation",
            reportTrackingId: "test-report",
            queryPhase: QueryPhase.Initial,
            queryType: FhirQueryType.Search);

    private static (
        Mock<IDistributedSemaphoreProvider> provider,
        Mock<IDistributedSemaphore> semaphore,
        Mock<IDistributedSynchronizationHandle> handle)
        SetupSemaphoreMocks()
    {
        var handle = new Mock<IDistributedSynchronizationHandle>();
        var semaphore = new Mock<IDistributedSemaphore>();
        var provider = new Mock<IDistributedSemaphoreProvider>();

        semaphore
            .Setup(s => s.AcquireAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IDistributedSynchronizationHandle>(handle.Object));
        provider
            .Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(semaphore.Object);

        return (provider, semaphore, handle);
    }

    private static Mock<IDataAcquisitionServiceMetrics> SetupMetricsMock()
    {
        var m = new Mock<IDataAcquisitionServiceMetrics>();
        m.Setup(x => x.MeasureDataRequestDuration(It.IsAny<List<KeyValuePair<string, object?>>>()))
            .Returns((TrackedRequestDuration)null!);
        return m;
    }

    private static Mock<IAuthenticationRetrievalService> SetupNoAuthMock()
    {
        var m = new Mock<IAuthenticationRetrievalService>();
        m.Setup(a => a.GetAuthenticationService(It.IsAny<AuthenticationConfigurationModel>()))
            .Returns((IAuth)null!);
        return m;
    }

    private static TestableSearchFhirCommand BuildSut(
        StubHttpMessageHandler stub,
        Mock<IDistributedSemaphoreProvider> semaphoreProvider,
        Mock<IDataAcquisitionServiceMetrics> metrics,
        Mock<IAuthenticationRetrievalService> auth)
    {
        var telemetrySettings = new Mock<IOptionsMonitor<TelemetrySettings>>();
        telemetrySettings
            .Setup(x => x.CurrentValue)
            .Returns(new TelemetrySettings());

        return new TestableSearchFhirCommand(
            new Mock<ILogger<SearchFhirCommand>>().Object,
            metrics.Object,
            semaphoreProvider.Object,
            Options.Create(new DistributedLockSettings()),
            auth.Object,
            telemetrySettings.Object,
            stub);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NoBundleNextLink_AcquiresSemaphoreOnceAndYieldsOneBundle()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(OkBundleResponse(BundleWithNoNext("b1")));

        var (semProvider, semaphore, handle) = SetupSemaphoreMocks();
        var metrics = SetupMetricsMock();
        var sut = BuildSut(stub, semProvider, metrics, SetupNoAuthMock());

        var results = new List<Bundle>();
        await foreach (var b in sut.ExecuteAsync(CreateRequest()))
            results.Add(b);

        Assert.Single(results);
        Assert.Equal("b1", results[0].Id);
        semaphore.Verify(
            s => s.AcquireAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once());
        handle.Verify(h => h.Dispose(), Times.Once());
        metrics.Verify(
            m => m.IncrementResourceAcquiredCounter(It.IsAny<List<KeyValuePair<string, object?>>>()),
            Times.Once());
    }

    [Fact]
    public async Task ExecuteAsync_ResourceAcquiredCounter_OmitsUnboundedIdentityLabels()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(OkBundleResponse(BundleWithNoNext("b1")));

        var (semProvider, _, _) = SetupSemaphoreMocks();
        var metrics = SetupMetricsMock();
        List<KeyValuePair<string, object?>>? capturedTags = null;
        metrics
            .Setup(m => m.IncrementResourceAcquiredCounter(It.IsAny<List<KeyValuePair<string, object?>>>()))
            .Callback<List<KeyValuePair<string, object?>>>(tags => capturedTags = tags);

        var sut = BuildSut(stub, semProvider, metrics, SetupNoAuthMock());

        await foreach (var _ in sut.ExecuteAsync(CreateRequest())) { }

        Assert.NotNull(capturedTags);
        Assert.Contains(capturedTags, tag => tag.Key == DiagnosticNames.FacilityId && (tag.Value?.ToString() ?? "") == "test-facility");
        Assert.Contains(capturedTags, tag => tag.Key == DiagnosticNames.ResourceType && (tag.Value?.ToString() ?? "") == "Patient");
        Assert.Contains(capturedTags, tag => tag.Key == DiagnosticNames.Phase);
        Assert.DoesNotContain(capturedTags, tag => tag.Key == DiagnosticNames.ResourceId);
        Assert.DoesNotContain(capturedTags, tag => tag.Key == DiagnosticNames.ReportTrackingId);
        Assert.DoesNotContain(capturedTags, tag => tag.Key == DiagnosticNames.PatientId);
        Assert.DoesNotContain(capturedTags, tag => tag.Key == DiagnosticNames.CorrelationId);
        Assert.DoesNotContain(capturedTags, tag => tag.Key == "patient_id");
        Assert.DoesNotContain(capturedTags, tag => tag.Key == "correlation_id");
    }

    [Fact]
    public async Task ExecuteAsync_ContinueAsyncReceives429_ThrowsTooManyRequestsExceptionWithParsedRetryAfter()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(OkBundleResponse(BundleWithNextLink("p1", "http://fhir.test/Patient?page=2")));
        stub.Enqueue(TooManyRequestsResponse(retryAfterSeconds: 30));

        var (semProvider, _, _) = SetupSemaphoreMocks();
        var sut = BuildSut(stub, semProvider, SetupMetricsMock(), SetupNoAuthMock());

        var ex = await Assert.ThrowsAsync<TooManyRequestsException>(async () =>
        {
            await foreach (var _ in sut.ExecuteAsync(CreateRequest())) { }
        });

        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
    }

    [Fact]
    public async Task ExecuteAsync_ContinueAsyncThrowsNon429Exception_PropagatesAndSemaphoreReleased()
    {
        // With Firely 5.x ContinueAsync always throws (never returns null) for
        // unexpected responses, so we test the general-exception propagation
        // branch: catch (Exception ex) { log; throw; }.
        // Verify: the first page is yielded, the semaphore slot is released
        // (using-block finalizer), and the exception escapes to the consumer.
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(OkBundleResponse(BundleWithNextLink("b1", "http://fhir.test/Patient?page=2")));
        stub.Enqueue(InternalServerErrorResponse());

        var (semProvider, semaphore, handle) = SetupSemaphoreMocks();
        var metrics = SetupMetricsMock();
        var sut = BuildSut(stub, semProvider, metrics, SetupNoAuthMock());

        var results = new List<Bundle>();
        FhirOperationException? caught = null;
        try
        {
            await foreach (var b in sut.ExecuteAsync(CreateRequest()))
                results.Add(b);
        }
        catch (FhirOperationException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsNotType<TooManyRequestsException>(caught);
        Assert.Equal(HttpStatusCode.InternalServerError, caught!.Status);
        Assert.Single(results);
        // Semaphore acquired twice (initial + paging), released twice even
        // though the second acquire threw during ContinueAsync.
        semaphore.Verify(
            s => s.AcquireAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        handle.Verify(h => h.Dispose(), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_Mrc1MultiPage_SemaphoreReleasedBeforeNextAcquireNoConcurrentDeadlock()
    {
        // With maxConcurrent=1 the slot must be released before it is
        // re-acquired on the next page turn.  We capture an acquire/release
        // trace to prove no self-deadlock can occur if an external consumer
        // were to use the slot between pages.
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(OkBundleResponse(BundleWithNextLink("p1", "http://fhir.test/Patient?page=2")));
        stub.Enqueue(OkBundleResponse(BundleWithNoNext("p2")));

        var trace = new List<string>();
        var provider = new Mock<IDistributedSemaphoreProvider>();
        var semaphore = new Mock<IDistributedSemaphore>();

        semaphore
            .Setup(s => s.AcquireAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                trace.Add("acquired");
                var h = new Mock<IDistributedSynchronizationHandle>();
                h.Setup(x => x.Dispose()).Callback(() => trace.Add("released"));
                return new ValueTask<IDistributedSynchronizationHandle>(h.Object);
            });
        provider
            .Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(semaphore.Object);

        var sut = BuildSut(stub, provider, SetupMetricsMock(), SetupNoAuthMock());

        var results = new List<Bundle>();
        await foreach (var b in sut.ExecuteAsync(CreateRequest(maxConcurrentRequests: 1)))
            results.Add(b);

        Assert.Equal(2, results.Count);
        Assert.Equal(new[] { "acquired", "released", "acquired", "released" }, trace);
    }

    [Fact]
    public async Task ExecuteAsync_QueryTypeSearchPost_SendsHttpPostRequest()
    {
        // SearchFhirCommand picks SearchUsingPostAsync vs SearchAsync purely on
        // request.queryType. SearchUsingPostAsync issues POST [base]/[type]/_search;
        // SearchAsync issues GET. Asserting the outgoing HTTP method proves the
        // SearchPost branch ran. IsReference is a FhirApiService concern and
        // never reaches this command, so it is not part of this assertion.
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(OkBundleResponse(BundleWithNoNext("b1")));

        var (semProvider, _, _) = SetupSemaphoreMocks();
        var sut = BuildSut(stub, semProvider, SetupMetricsMock(), SetupNoAuthMock());

        var request = CreateRequest() with { queryType = FhirQueryType.SearchPost };

        await foreach (var _ in sut.ExecuteAsync(request)) { }

        var captured = Assert.Single(stub.CapturedRequests);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.EndsWith("_search", captured.RequestUri!.AbsolutePath);
    }
}
