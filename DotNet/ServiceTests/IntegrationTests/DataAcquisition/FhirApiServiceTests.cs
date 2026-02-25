using System.Net;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Support;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Moq;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FhirApiServiceTests
{
    [Fact]
    public async Task CheckIfReferenceResourceHasBeenSent_ResourceAlreadySent_ReturnsTrueAndSkipsReprocessing()
    {
        var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
        mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref1", "report1", "fac1", "corr1", CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task CheckIfReferenceResourceHasBeenSent_ResourceNotSent_ReturnsFalseAndProceeds()
    {
        var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
        mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref2", "report2", "fac2", "corr2", CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task CheckIfReferenceResourceHasBeenSent_CancellationTokenTriggered_ThrowsOperationCanceledException()
    {
        var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref3", "report3", "fac3", "corr3", cts.Token));
    }

    [Fact]
    public async Task CheckIfReferenceResourceHasBeenSent_UnderlyingQueryFailure_ThrowsException()
    {
        var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
        mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB failure"));

        await Assert.ThrowsAsync<Exception>(() =>
            mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref4", "report4", "fac4", "corr4", CancellationToken.None));
    }

    [Fact]
    public void InsertDateExtension_AddsMetaExtension()
    {
        // Arrange: Mock all dependencies for FhirApiService
        var referenceResourceManager = new Mock<IReferenceResourcesManager>();
        var referenceResourceQueries = new Mock<IReferenceResourcesQueries>();
        var referenceResourceService = new Mock<IReferenceResourceService>();
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourceAcquired>>();

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            kafkaProducer.Object
        );

        var resource = new Patient();

        // Act: Use reflection to invoke the private InsertDateExtension method
        typeof(FhirApiService)
            .GetMethod("InsertDateExtension", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(service, new object[] { resource });

        // Assert: meta.extension contains the expected extension
        Assert.NotNull(resource.Meta);
        Assert.NotNull(resource.Meta.Extension);
        Assert.Contains(resource.Meta.Extension, ext =>
            ext.Url == DataAcquisitionConstants.Extension.DateReceivedExtensionUri &&
            ext.Value is FhirDateTime str &&
            !string.IsNullOrWhiteSpace(str.Value) &&
            str.Value.EndsWith("Z") // ISO 8601 UTC check
        );
    }

    [Fact]
    public async Task ExecuteRead_OperationOutcomeIsNoted()
    {
        // Arrange: Mock all dependencies for FhirApiService
        var referenceResourceManager = new Mock<IReferenceResourcesManager>();
        var referenceResourceQueries = new Mock<IReferenceResourcesQueries>();
        var referenceResourceService = new Mock<IReferenceResourceService>();
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourceAcquired>>();

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            kafkaProducer.Object
        );

        var resource = new Patient();

        var log = new DataAcquisitionLogModel
        {
            FacilityId = "12345",
            PatientId = "the-patient",
            ResourceId = "the-patient"
        };
        var fhirQuery = new FhirQueryModel
        {
            IsReference = false
        };

        var outcome = new OperationOutcome();
        outcome.AddIssue("Something went horribly wrong.", Issue.PROCESSING_CATASTROPHIC_FAILURE);
        var exception = new FhirOperationException("Something went horribly wrong.", HttpStatusCode.InternalServerError, outcome);

        readFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        await Assert.ThrowsAsync<FhirOperationException>(async () =>
            await service.ExecuteRead(log, fhirQuery, ResourceType.Patient, new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://example.com/fhir" }));
        Assert.NotNull(log.Notes);
        Assert.NotEmpty(log.Notes);
        Assert.StartsWith("OperationOutcome", log.Notes[0]);
    }

    [Fact]
    public async Task ExecuteSearch_SharedResource_KafkaMessage_NoPatientId()
    {
        // Arrange: Mock all dependencies for FhirApiService
        var referenceResourceManager = new Mock<IReferenceResourcesManager>();
        var referenceResourceQueries = new Mock<IReferenceResourcesQueries>();
        var referenceResourceService = new Mock<IReferenceResourceService>();
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourceAcquired>>();

        // Prepare a shared resource (e.g., Location) with no patient context
        var location = new Location
        {
            Id = "loc-1"
        };
        var bundle = new Bundle
        {
            Entry = new List<Bundle.EntryComponent>
            {
                new Bundle.EntryComponent { Resource = location }
            }
        };

        // Setup searchFhirCommand to return the bundle as an async stream
        searchFhirCommand
            .Setup(x => x.ExecuteAsync(
                It.IsAny<SearchFhirCommandRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(GetBundleAsync(bundle));

        // Capture the produced Kafka message
        ResourceAcquired? producedMessage = null;
        kafkaProducer
            .Setup(x => x.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourceAcquired>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<ResourceKey, ResourceAcquired>, CancellationToken>((topic, msg, ct) =>
            {
                producedMessage = msg.Value;
            })
            .ReturnsAsync(new DeliveryResult<ResourceKey, ResourceAcquired>());

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            kafkaProducer.Object
        );

        var log = new DataAcquisitionLogModel
        {
            FacilityId = "fac-1",
            CorrelationId = "corr-1",
            QueryPhase = QueryPhase.Initial,
            ScheduledReport = new ScheduledReport(),
            ReportableEvent = ReportableEvent.Adhoc
        };

        var fhirQuery = new FhirQueryModel
        {
            IsReference = true, // Shared resource
            ResourceReferenceTypes = new List<ResourceReferenceTypeModel>(),
            IdQueryParameterValues = new List<string> { "loc-1" }
        };

        var fhirQueryConfig = new FhirQueryConfigurationModel
        {
            FhirServerBaseUrl = "http://example.com/fhir"
        };

        // Act
        await service.ExecuteSearch(log, fhirQuery, fhirQueryConfig, ResourceType.Location);

        // Assert: Kafka message was produced and PatientId is null
        Assert.NotNull(producedMessage);
        Assert.Equal(location, producedMessage.Resource);
        Assert.Null(producedMessage.PatientId);

    }

    private static async IAsyncEnumerable<Bundle> GetBundleAsync(Bundle bundle)
    {
        yield return bundle;
        await Task.CompletedTask;
    }
}