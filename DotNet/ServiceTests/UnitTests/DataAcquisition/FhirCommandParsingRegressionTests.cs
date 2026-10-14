using System.Net;
using System.Text;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Medallion.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class FhirCommandParsingRegressionTests
{
    // LEGLINK-1449: keep this as raw JSON; serializing a Patient would pad _given
    // and remove the malformed EHR response that caused the regression.
    private static string PatientJson(bool padGivenMetadata) => $$"""
        {
          "resourceType": "Patient",
          "id": "test-patient",
          "name": [{
            "use": "usual",
            "text": "Some Name LastName",
            "family": "LastName",
            "given": ["Some", "Name"],
            "_given": [{
              "extension": [{
                "valueCode": "CL",
                "url": "http://hl7.org/fhir/StructureDefinition/iso21090-EN-qualifier"
              }]
            }{{(padGivenMetadata ? ", null" : "")}}]
          }]
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_PatientGivenMetadata_ReturnsBothGivenNames(bool padGivenMetadata)
    {
        using var handler = new JsonResponseHandler(PatientJson(padGivenMetadata));
        using var httpClient = new HttpClient();
        var sut = new TestableReadFhirCommand(httpClient, CreateSemaphoreProvider(), handler);
        var request = new ReadFhirCommandRequest(
            "test-facility", ResourceType.Patient, "test-patient",
            "http://fhir.test", CreateQueryConfiguration(), "test-report");

        var resource = await sut.ExecuteAsync(request, CancellationToken.None);

        AssertPatient(resource);
        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal("/Patient/test-patient", handler.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(false, false, FhirQueryType.Search)]
    [InlineData(false, false, FhirQueryType.SearchPost)]
    [InlineData(false, true, FhirQueryType.Search)]
    [InlineData(true, false, FhirQueryType.Search)]
    [InlineData(true, false, FhirQueryType.SearchPost)]
    [InlineData(true, true, FhirQueryType.Search)]
    public async Task Search_PatientGivenMetadata_ReturnsBothGivenNames(
        bool padGivenMetadata, bool nonPaging, FhirQueryType queryType)
    {
        var bundleJson = $$"""
            {
              "resourceType": "Bundle",
              "type": "searchset",
              "total": 1,
              "entry": [{
                "fullUrl": "http://fhir.test/Patient/test-patient",
                "resource": {{PatientJson(padGivenMetadata)}}
              }]
            }
            """;
        using var handler = new JsonResponseHandler(bundleJson);
        using var httpClient = new HttpClient();
        var sut = new TestableSearchFhirCommand(httpClient, CreateSemaphoreProvider(), handler);
        var request = new SearchFhirCommandRequest(
            CreateQueryConfiguration(), ResourceType.Patient, new SearchParams(),
            "test-facility", "test-patient", "test-correlation", QueryPhase.Initial,
            queryType, "test-report");

        Bundle bundle;
        if (nonPaging)
        {
            bundle = await sut.ExecuteNonPagingAsync(request, CancellationToken.None);
        }
        else
        {
            var bundles = new List<Bundle>();
            await foreach (var result in sut.ExecuteAsync(request, CancellationToken.None))
                bundles.Add(result);
            bundle = Assert.Single(bundles);
        }

        AssertPatient(Assert.Single(bundle.Entry).Resource);
        Assert.Equal(queryType == FhirQueryType.SearchPost ? HttpMethod.Post : HttpMethod.Get,
            handler.RequestMethod);
        Assert.Equal(queryType == FhirQueryType.SearchPost ? "/Patient/_search" : "/Patient",
            handler.RequestUri!.AbsolutePath);
    }

    private static void AssertPatient(Resource? resource)
    {
        var patient = Assert.IsType<Patient>(resource);
        Assert.Equal("test-patient", patient.Id);
        var name = Assert.Single(patient.Name);
        Assert.Equal(new[] { "Some", "Name" }, name.Given);
    }

    private static FhirQueryConfigurationModel CreateQueryConfiguration() => new()
    {
        FhirServerBaseUrl = "http://fhir.test",
        MaxConcurrentRequests = 1,
        Authentication = null
    };

    private static IDistributedSemaphoreProvider CreateSemaphoreProvider()
    {
        var semaphore = new Mock<IDistributedSemaphore>();
        semaphore
            .Setup(s => s.AcquireAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IDistributedSynchronizationHandle>(
                new Mock<IDistributedSynchronizationHandle>().Object));
        var provider = new Mock<IDistributedSemaphoreProvider>();
        provider.Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(semaphore.Object);
        return provider.Object;
    }

    private sealed class JsonResponseHandler(string json) : HttpMessageHandler
    {
        public HttpMethod? RequestMethod { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/fhir+json")
            });
        }
    }

    private sealed class TestableReadFhirCommand(
        HttpClient httpClient, IDistributedSemaphoreProvider semaphoreProvider,
        HttpMessageHandler handler)
        : ReadFhirCommand(NullLogger<ReadFhirCommand>.Instance, httpClient, semaphoreProvider,
            Options.Create(new DistributedLockSettings()),
            Mock.Of<IAuthenticationRetrievalService>())
    {
        protected override HttpMessageHandler CreateInnerHttpMessageHandler() => handler;
    }

    private sealed class TestableSearchFhirCommand(
        HttpClient httpClient, IDistributedSemaphoreProvider semaphoreProvider,
        HttpMessageHandler handler)
        : SearchFhirCommand(NullLogger<SearchFhirCommand>.Instance, httpClient,
            Mock.Of<IDataAcquisitionServiceMetrics>(), semaphoreProvider,
            Options.Create(new DistributedLockSettings()),
            Mock.Of<IAuthenticationRetrievalService>(), CreateTelemetrySettings())
    {
        protected override HttpMessageHandler CreateInnerHttpMessageHandler() => handler;

        private static IOptionsMonitor<TelemetrySettings> CreateTelemetrySettings()
        {
            var settings = new Mock<IOptionsMonitor<TelemetrySettings>>();
            settings.Setup(s => s.CurrentValue).Returns(new TelemetrySettings());
            return settings.Object;
        }
    }
}
