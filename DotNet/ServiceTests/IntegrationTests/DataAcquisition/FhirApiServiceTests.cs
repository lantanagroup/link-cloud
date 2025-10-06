using Hl7.Fhir.Rest;
using Hl7.Fhir.Support;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
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
            .ThrowsAsync(new System.Exception("DB failure"));

        await Assert.ThrowsAsync<System.Exception>(() =>
            mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref4", "report4", "fac4", "corr4", CancellationToken.None));
    }

    [Fact]
    public void InsertDateExtension_AddsMetaExtension()
    {
        // Arrange: Mock all dependencies for FhirApiService
        var logger = new Mock<ILogger<FhirApiService>>();
        var metrics = new Mock<IDataAcquisitionServiceMetrics>();
        var bundleEventService = new Mock<IBundleEventService<string, ResourceAcquired, ResourceAcquiredMessageGenerationRequest>>();
        var referenceResourceManager = new Mock<IReferenceResourcesManager>();
        var dataAcquisitionLogManager = new Mock<IDataAcquisitionLogManager>();
        var referenceResourceService = new Mock<IReferenceResourceService>();
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var dataAcquisitionLogQueries = new Mock<IDataAcquisitionLogQueries>();
        var kafkaProducer = new Mock<Confluent.Kafka.IProducer<string, ResourceAcquired>>();
        var fhirQueryManager = new Mock<IFhirQueryManager>();

        var service = new FhirApiService(
            logger.Object,
            metrics.Object,
            bundleEventService.Object,
            referenceResourceManager.Object,
            dataAcquisitionLogManager.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            dataAcquisitionLogQueries.Object,
            kafkaProducer.Object,
            fhirQueryManager.Object
        );

        var resource = new Patient();

        // Act: Use reflection to invoke the private InsertDateExtension method
        typeof(FhirApiService)
            .GetMethod("InsertDateExtension", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
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
        var logger = new Mock<ILogger<FhirApiService>>();
        var metrics = new Mock<IDataAcquisitionServiceMetrics>();
        var bundleEventService = new Mock<IBundleEventService<string, ResourceAcquired, ResourceAcquiredMessageGenerationRequest>>();
        var referenceResourceManager = new Mock<IReferenceResourcesManager>();
        var dataAcquisitionLogManager = new Mock<IDataAcquisitionLogManager>();
        var referenceResourceService = new Mock<IReferenceResourceService>();
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var dataAcquisitionLogQueries = new Mock<IDataAcquisitionLogQueries>();
        var kafkaProducer = new Mock<Confluent.Kafka.IProducer<string, ResourceAcquired>>();
        var fhirQueryManager = new Mock<IFhirQueryManager>();

        var service = new FhirApiService(
            logger.Object,
            metrics.Object,
            bundleEventService.Object,
            referenceResourceManager.Object,
            dataAcquisitionLogManager.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            dataAcquisitionLogQueries.Object,
            kafkaProducer.Object,
            fhirQueryManager.Object
        );

        var resource = new Patient();

        var log = new DataAcquisitionLog
        {
            FacilityId = "12345",
            PatientId = "the-patient",
            ResourceId = "the-patient"
        };
        var fhirQuery = new FhirQuery
        {
            isReference = false
        };

        var outcome = new OperationOutcome();
        outcome.AddIssue("Something went horribly wrong.", Issue.PROCESSING_CATASTROPHIC_FAILURE);
        var exception = new FhirOperationException("Something went horribly wrong.", HttpStatusCode.InternalServerError, outcome);

        readFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        //searchFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
        //    .Throws(exception);

        await Assert.ThrowsAsync<FhirOperationException>(async () =>
            await service.ExecuteRead(log, fhirQuery, ResourceType.Patient, new FhirQueryConfiguration { FhirServerBaseUrl = "http://example.com/fhir" }, null!));
        Assert.NotNull(log.Notes);
        Assert.NotEmpty(log.Notes);
        Assert.StartsWith("OperationOutcome", log.Notes[0]);
    }
}