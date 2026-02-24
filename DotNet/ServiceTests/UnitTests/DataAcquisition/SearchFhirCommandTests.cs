using System.Net;
using System.Text;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Commands;

public class SearchFhirCommandTests
{
    private readonly Mock<ILogger<SearchFhirCommand>> _loggerMock;
    private readonly Mock<IDataAcquisitionServiceMetrics> _metricsMock;
    private readonly Mock<IDistributedSemaphoreProvider> _semaphoreProviderMock;
    private readonly Mock<IAuthenticationRetrievalService> _authServiceMock;
    private readonly IOptions<DistributedLockSettings> _lockSettings;

    public SearchFhirCommandTests()
    {
        _loggerMock = new Mock<ILogger<SearchFhirCommand>>();
        _metricsMock = new Mock<IDataAcquisitionServiceMetrics>();
        _semaphoreProviderMock = new Mock<IDistributedSemaphoreProvider>();
        _authServiceMock = new Mock<IAuthenticationRetrievalService>();
        _lockSettings = Options.Create(new DistributedLockSettings { Expiration = TimeSpan.FromMinutes(1) });

        var semaphoreMock = new Mock<IDistributedSemaphore>();
        semaphoreMock.Setup(x => x.Acquire(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(new Mock<IDistributedSynchronizationHandle>().Object);

        _semaphoreProviderMock.Setup(x => x.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(semaphoreMock.Object);
    }

    private class TestSearchFhirCommand : SearchFhirCommand
    {
        public HttpMessageHandler HttpMessageHandler { get; set; }

        public TestSearchFhirCommand(ILogger<SearchFhirCommand> logger, HttpClient httpClient, IDataAcquisitionServiceMetrics metrics, IDistributedSemaphoreProvider distributedSemaphoreProvider, IOptions<DistributedLockSettings> distributedLockSettings, IAuthenticationRetrievalService authenticationRetrievalService) 
            : base(logger, httpClient, metrics, distributedSemaphoreProvider, distributedLockSettings, authenticationRetrievalService)
        {
        }

        protected override (HttpClient client, HeaderCapturingHandler handler) CreateHttpClientWithHandler()
        {
            var headerCapturingHandler = new HeaderCapturingHandler { InnerHandler = HttpMessageHandler };
            var httpClientWithHandler = new HttpClient(headerCapturingHandler);
            return (httpClientWithHandler, headerCapturingHandler);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Search_ResultInCorrectGetRequest()
    {
        // Setup
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(new FhirJsonSerializer().SerializeToString(new Bundle()), Encoding.UTF8, "application/fhir+json")
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var request = new SearchFhirCommandRequest(
            new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test.com" },
            ResourceType.MedicationRequest,
            new SearchParams().Add("param1", "value1"),
            "facility-1",
            "patient-1",
            "correlation-1",
            QueryPhase.Initial,
            FhirQueryType.Search
        );

        var command = new TestSearchFhirCommand(_loggerMock.Object, new HttpClient(), _metricsMock.Object, _semaphoreProviderMock.Object, _lockSettings, _authServiceMock.Object)
        {
            HttpMessageHandler = handlerMock.Object
        };

        // Execute
        var results = new List<Bundle>();
        await foreach (var bundle in command.ExecuteAsync(request))
        {
            results.Add(bundle);
        }

        // Verify
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.ToString().Contains("MedicationRequest") &&
                req.RequestUri.Query.Contains("param1=value1")),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_SearchPost_ResultInCorrectPostRequest()
    {
        // Setup
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(new FhirJsonSerializer().SerializeToString(new Bundle()), Encoding.UTF8, "application/fhir+json")
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var request = new SearchFhirCommandRequest(
            new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test.com" },
            ResourceType.MedicationRequest,
            new SearchParams().Add("param1", "value1"),
            "facility-1",
            "patient-1",
            "correlation-1",
            QueryPhase.Initial,
            FhirQueryType.SearchPost
        );

        var command = new TestSearchFhirCommand(_loggerMock.Object, new HttpClient(), _metricsMock.Object, _semaphoreProviderMock.Object, _lockSettings, _authServiceMock.Object)
        {
            HttpMessageHandler = handlerMock.Object
        };

        // Execute
        var results = new List<Bundle>();
        await foreach (var bundle in command.ExecuteAsync(request))
        {
            results.Add(bundle);
        }

        // Verify
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri.ToString().Contains("MedicationRequest/_search") &&
                req.Content.ReadAsStringAsync().Result.Contains("param1=value1")),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_Paging_ResultInMultipleRequests()
    {
        // Setup
        var handlerMock = new Mock<HttpMessageHandler>();
        
        var bundle1 = new Bundle();
        bundle1.Entry.Add(new Bundle.EntryComponent { Resource = new Patient { Id = "patient-1" } });
        bundle1.Link.Add(new Bundle.LinkComponent { Relation = "next", Url = "http://test.com/next-page" });
        
        var bundle2 = new Bundle();
        bundle2.Entry.Add(new Bundle.EntryComponent { Resource = new Patient { Id = "patient-2" } });

        var response1 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(new FhirJsonSerializer().SerializeToString(bundle1), Encoding.UTF8, "application/fhir+json")
        };

        var response2 = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(new FhirJsonSerializer().SerializeToString(bundle2), Encoding.UTF8, "application/fhir+json")
        };

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response1)
            .ReturnsAsync(response2);

        var request = new SearchFhirCommandRequest(
            new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test.com" },
            ResourceType.MedicationRequest,
            new SearchParams(),
            "facility-1",
            "patient-1",
            "correlation-1",
            QueryPhase.Initial,
            FhirQueryType.SearchPost // Start with POST
        );

        var command = new TestSearchFhirCommand(_loggerMock.Object, new HttpClient(), _metricsMock.Object, _semaphoreProviderMock.Object, _lockSettings, _authServiceMock.Object)
        {
            HttpMessageHandler = handlerMock.Object
        };

        // Execute
        var results = new List<Bundle>();
        await foreach (var bundle in command.ExecuteAsync(request))
        {
            results.Add(bundle);
        }

        // Verify
        Assert.Equal(2, results.Count);
        
        // Verify first request was POST
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri.ToString().Contains("MedicationRequest/_search")),
            ItExpr.IsAny<CancellationToken>()
        );

        // Verify second request was GET to the next-page URL
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.ToString() == "http://test.com/next-page"),
            ItExpr.IsAny<CancellationToken>()
        );
    }
    [Fact]
    public async Task ExecuteNonPagingAsync_SearchPost_ResultInCorrectPostRequest()
    {
        // Setup
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(new FhirJsonSerializer().SerializeToString(new Bundle()), Encoding.UTF8, "application/fhir+json")
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var request = new SearchFhirCommandRequest(
            new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test.com" },
            ResourceType.MedicationRequest,
            new SearchParams().Add("param1", "value1"),
            "facility-1",
            "patient-1",
            "correlation-1",
            QueryPhase.Initial,
            FhirQueryType.SearchPost
        );

        var command = new TestSearchFhirCommand(_loggerMock.Object, new HttpClient(), _metricsMock.Object, _semaphoreProviderMock.Object, _lockSettings, _authServiceMock.Object)
        {
            HttpMessageHandler = handlerMock.Object
        };

        // Execute
        await command.ExecuteNonPagingAsync(request, CancellationToken.None);

        // Verify
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri.ToString().Contains("MedicationRequest/_search") &&
                req.Content.ReadAsStringAsync().Result.Contains("param1=value1")),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}
