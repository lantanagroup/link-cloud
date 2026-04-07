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
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
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
        var logger = new Mock<ILogger<FhirApiService>>();

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            kafkaProducer.Object,
            logger.Object
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
        var logger = new Mock<ILogger<FhirApiService>>();

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            kafkaProducer.Object,
            logger.Object
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
        outcome.Issue.Add(new OperationOutcome.IssueComponent
        {
            Severity = OperationOutcome.IssueSeverity.Fatal,
            Code = OperationOutcome.IssueType.Processing,
            Diagnostics = "Something went horribly wrong."
        });
        var exception = new FhirOperationException("Something went horribly wrong.", HttpStatusCode.InternalServerError, outcome);

        readFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        await Assert.ThrowsAsync<OpOutcomeException>(async () =>
            await service.ExecuteRead(log, fhirQuery, ResourceType.Patient, new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://example.com/fhir" }));
        Assert.NotNull(log.Notes);
        Assert.NotEmpty(log.Notes);
        Assert.Contains("HTTP InternalServerError returned for Read operation", log.Notes[0]);
    }

    [Fact]
    public async Task ExecuteSearch_OperationOutcomeIsNoted()
    {
        // Arrange: Mock all dependencies for FhirApiService
        var referenceResourceManager = new Mock<IReferenceResourcesManager>();
        var referenceResourceQueries = new Mock<IReferenceResourcesQueries>();
        var referenceResourceService = new Mock<IReferenceResourceService>();
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourceAcquired>>();
        var logger = new Mock<ILogger<FhirApiService>>();

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            referenceResourceService.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            kafkaProducer.Object,
            logger.Object
        );

        var log = new DataAcquisitionLogModel
        {
            FacilityId = "12345",
            CorrelationId = "corr-1"
        };
        var fhirQuery = new FhirQueryModel
        {
            IsReference = false
        };

        var outcome = new OperationOutcome();
        outcome.Issue.Add(new OperationOutcome.IssueComponent
        {
            Severity = OperationOutcome.IssueSeverity.Error,
            Code = OperationOutcome.IssueType.Processing,
            Diagnostics = "Something went wrong during search."
        });
        var exception = new FhirOperationException("Search failed", HttpStatusCode.BadRequest, outcome);

        searchFhirCommand
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetExceptionBundleAsync(exception));

        await Assert.ThrowsAsync<OpOutcomeException>(async () =>
            await service.ExecuteSearch(log, fhirQuery, new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://example.com/fhir" }, ResourceType.Patient));

        Assert.NotNull(log.Notes);
        Assert.NotEmpty(log.Notes);
        Assert.Contains("HTTP BadRequest returned for Search operation", log.Notes[0]);
    }

    [Fact]
    public async Task ExecuteRead_NotFound_ThrowsOpOutcomeException()
    {
        // Arrange
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            new Mock<IReferenceResourceService>().Object,
            new Mock<ISearchFhirCommand>().Object,
            readFhirCommand.Object,
            new Mock<IProducer<ResourceKey, ResourceAcquired>>().Object,
            new Mock<ILogger<FhirApiService>>().Object
        );

        var log = new DataAcquisitionLogModel { FacilityId = "123", ResourceId = "res-1" };
        var fhirQuery = new FhirQueryModel { IsReference = false };
        var exception = new FhirOperationException("Not Found", HttpStatusCode.NotFound);

        readFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<OpOutcomeException>(async () =>
            await service.ExecuteRead(log, fhirQuery, ResourceType.Patient, new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" }));

        Assert.Contains("HTTP NotFound returned", log.Notes[0]);
    }

    [Fact]
    public async Task ExecuteSearch_Gone_ThrowsOpOutcomeException()
    {
        // Arrange
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            new Mock<IReferenceResourceService>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<IProducer<ResourceKey, ResourceAcquired>>().Object,
            new Mock<ILogger<FhirApiService>>().Object
        );

        var log = new DataAcquisitionLogModel { FacilityId = "123", CorrelationId = "c-1" };
        var fhirQuery = new FhirQueryModel { IsReference = false };
        var exception = new FhirOperationException("Gone", HttpStatusCode.Gone);

        searchFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetExceptionBundleAsync(exception));

        // Act & Assert
        await Assert.ThrowsAsync<OpOutcomeException>(async () =>
            await service.ExecuteSearch(log, fhirQuery, new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" }, ResourceType.Patient));

        Assert.Contains("HTTP Gone returned for Search operation", log.Notes[0]);
    }

    [Fact]
    public async Task ExecuteSearch_SkipsOperationOutcome()
    {
        // Arrange
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourceAcquired>>();
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            new Mock<IReferenceResourceService>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            kafkaProducer.Object,
            new Mock<ILogger<FhirApiService>>().Object
        );

        var patient = new Patient { Id = "p1" };
        var outcome = new OperationOutcome { Id = "o1" };
        var bundle = new Bundle
        {
            Entry = new List<Bundle.EntryComponent>
            {
                new Bundle.EntryComponent { Resource = patient },
                new Bundle.EntryComponent { Resource = outcome }
            }
        };

        searchFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetBundleAsync(bundle));

        var log = new DataAcquisitionLogModel { FacilityId = "123", CorrelationId = "c1", ScheduledReport = new ScheduledReport(), ReportableEvent = ReportableEvent.Adhoc };
        var fhirQuery = new FhirQueryModel { IsReference = false, ResourceReferenceTypes = new List<ResourceReferenceTypeModel>() };

        // Act
        var ids = await service.ExecuteSearch(log, fhirQuery, new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" }, ResourceType.Patient);

        // Assert
        Assert.Single(ids);
        Assert.Equal("Patient/p1", ids.First());
        Assert.NotNull(log.Notes);
        Assert.Contains(log.Notes, n => n.Contains("OperationOutcome(s) found in search bundle"));

        // Ensure only Patient was produced to Kafka
        kafkaProducer.Verify(x => x.ProduceAsync(
            It.IsAny<string>(),
            It.Is<Message<ResourceKey, ResourceAcquired>>(m => m.Value.Resource.TypeName == "Patient"),
            It.IsAny<CancellationToken>()), Times.Once);

        kafkaProducer.Verify(x => x.ProduceAsync(
            It.IsAny<string>(),
            It.Is<Message<ResourceKey, ResourceAcquired>>(m => m.Value.Resource.TypeName == "OperationOutcome"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private async IAsyncEnumerable<Bundle> GetExceptionBundleAsync(Exception ex)
    {
        await Task.CompletedTask;
        throw ex;
        yield break;
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
        var logger = new Mock<ILogger<FhirApiService>>();

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
            kafkaProducer.Object,
            logger.Object
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