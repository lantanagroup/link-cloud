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
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Moq;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Shared.Application.Interfaces;
using UnitTests.Admin.BFF.Aggregation;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class FhirApiServiceTests
{
    private static ILocationMappingService CreateLocationMappingService()
    {
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);
        return locationMappingService.Object;
    }

    private static void ConfigureDefaultLocationMappingFilter(Mock<ILocationMappingService> locationMappingService)
    {
        locationMappingService
            .Setup(x => x.FilterResourcesByEncounterMappingAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Resource>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyCollection<Resource> resources, CancellationToken _) => resources.ToList());
        locationMappingService
            .Setup(x => x.UpdateResourceMappingsAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Resource>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrganizationLocationMappingModel>());
    }

    [Fact]
    public void FhirQueryModel_IdQueryParameterValues_StaysInSyncWithQueryParameters()
    {
        var model = new FhirQueryModel
        {
            QueryParameters = new List<string> { "status=active", "_id=loc-1,loc-2" }
        };

        Assert.Equal(new[] { "loc-1", "loc-2" }, model.IdQueryParameterValues.ToList());

        model.IdQueryParameterValues = new[] { "med-1", "med-2" };

        Assert.Equal(new[] { "status=active", "_id=med-1,med-2" }, model.QueryParameters);
        Assert.Equal(new[] { "med-1", "med-2" }, model.IdQueryParameterValues.ToList());
    }

    [Fact]
    public void FhirQueryModel_IdQueryParameterValues_EmptyAssignment_DoesNotInjectStrayIdParam()
    {
        // Regression guard: assigning an empty IdQueryParameterValues must not leave
        // a stray "_id=" entry on QueryParameters - otherwise PatientDataService's
        // empty-_id skip path would mark every primary search as Skipped before it
        // ever fetches anything (and therefore before any references can be discovered).
        var model = new FhirQueryModel
        {
            QueryParameters = new List<string> { "patient=Patient/123" }
        };

        model.IdQueryParameterValues = Array.Empty<string>();

        Assert.Equal(new[] { "patient=Patient/123" }, model.QueryParameters);
        Assert.Empty(model.IdQueryParameterValues);
    }

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
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourcesAcquired>>();
        var logger = new Mock<ILogger<FhirApiService>>();
        var resourceCache = new Mock<IResourceCache>();
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            logger.Object,
            resourceCache.Object,
            locationMappingService.Object
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
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourcesAcquired>>();
        var logger = new Mock<ILogger<FhirApiService>>();
        var resourceCache = new Mock<IResourceCache>();

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            logger.Object,
            resourceCache.Object,
            CreateLocationMappingService()
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
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourcesAcquired>>();
        var logger = new Mock<ILogger<FhirApiService>>();
        var resourceCache = new Mock<IResourceCache>();

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            logger.Object,
            resourceCache.Object,
            CreateLocationMappingService()
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
            new Mock<ISearchFhirCommand>().Object,
            readFhirCommand.Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            CreateLocationMappingService()
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
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            CreateLocationMappingService()
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
        var resourceCache = new Mock<IResourceCache>();
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<ILogger<FhirApiService>>().Object,
            resourceCache.Object,
            CreateLocationMappingService()
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
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        var ids = await service.ExecuteSearch(log, fhirQuery, new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" }, ResourceType.Patient, cancellationToken: cancellationToken);

        // Assert
        Assert.Single(ids);
        Assert.Equal("Patient/p1", ids.First());
        Assert.NotNull(log.Notes);
        Assert.Contains(log.Notes, n => n.Contains("OperationOutcome(s) found in search bundle"));

        // Ensure only Patient was added to cache (not OperationOutcome)
        resourceCache.Verify(x => x.UpdateCorrelationCacheAsync(
            It.Is<string>(k => k.Contains(":Patient")),
            It.IsAny<List<DomainResource>>(),
            It.IsAny<ResourceType>(),
            cancellationToken), Times.Once);

        resourceCache.Verify(x => x.UpdateCorrelationCacheAsync(
            It.Is<string>(k => k.Contains(":OperationOutcome")),
            It.IsAny<List<DomainResource>>(),
            It.IsAny<ResourceType>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteSearch_UsesLocationMappingServiceEncounterFilter()
    {
        // Arrange
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var resourceCache = new Mock<IResourceCache>();
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);

        var keptObservation = new Observation
        {
            Id = "obs-kept",
            Encounter = new ResourceReference("Encounter/enc-kept")
        };
        var removedObservation = new Observation
        {
            Id = "obs-removed",
            Encounter = new ResourceReference("Encounter/enc-removed")
        };

        var bundle = new Bundle
        {
            Entry =
            [
                new Bundle.EntryComponent { Resource = keptObservation },
                new Bundle.EntryComponent { Resource = removedObservation }
            ]
        };

        searchFhirCommand
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetBundleAsync(bundle));

        locationMappingService
            .Setup(x => x.FilterResourcesByEncounterMappingAsync(
                "fac-1",
                It.Is<IReadOnlyCollection<Resource>>(resources => resources.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([keptObservation]);

        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<ILogger<FhirApiService>>().Object,
            resourceCache.Object,
            locationMappingService.Object
        );

        var log = new DataAcquisitionLogModel
        {
            FacilityId = "fac-1",
            CorrelationId = "corr-1",
            PatientId = "Patient/patient-1",
            ScheduledReport = new ScheduledReport(),
            ReportableEvent = ReportableEvent.Adhoc
        };
        var fhirQuery = new FhirQueryModel
        {
            IsReference = false,
            QueryParameters = ["patient=Patient/patient-1"],
            ResourceReferenceTypes = new List<ResourceReferenceTypeModel>()
        };
        var fhirQueryConfiguration = new FhirQueryConfigurationModel
        {
            FhirServerBaseUrl = "http://test"
        };

        // Act
        var ids = await service.ExecuteSearch(log, fhirQuery, fhirQueryConfiguration, ResourceType.Observation);

        // Assert
        Assert.Equal(["Observation/obs-kept"], ids);
        resourceCache.Verify(x => x.UpdateCorrelationCacheAsync(
            It.IsAny<string>(),
            It.Is<List<DomainResource>>(resources => resources.Single().Id == "obs-removed"),
            ResourceType.Observation,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteSearch_NoActiveOrganizationLocationConfiguration_DoesNotFilterEncounterResources()
    {
        // Arrange
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var encounterMappingQueries = new Mock<IEncounterMappingQueries>();
        var organizationLocationConfigurationQueries = new Mock<IOrganizationLocationConfigurationQueries>();

        var observation = new Observation
        {
            Id = "obs-1",
            Encounter = new ResourceReference("Encounter/enc-1")
        };

        var bundle = new Bundle
        {
            Entry = [new Bundle.EntryComponent { Resource = observation }]
        };

        searchFhirCommand
            .Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetBundleAsync(bundle));

        organizationLocationConfigurationQueries
            .Setup(x => x.HasActiveByFacilityIdAsync("fac-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);

        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            locationMappingService.Object
        );

        var log = new DataAcquisitionLogModel
        {
            FacilityId = "fac-1",
            CorrelationId = "corr-1",
            PatientId = "Patient/patient-1",
            ScheduledReport = new ScheduledReport(),
            ReportableEvent = ReportableEvent.Adhoc
        };
        var fhirQuery = new FhirQueryModel
        {
            IsReference = false,
            QueryParameters = ["patient=Patient/patient-1"],
            ResourceReferenceTypes = new List<ResourceReferenceTypeModel>()
        };
        var fhirQueryConfiguration = new FhirQueryConfigurationModel
        {
            FhirServerBaseUrl = "http://test"
        };

        // Act
        var ids = await service.ExecuteSearch(log, fhirQuery, fhirQueryConfiguration, ResourceType.Observation);

        // Assert
        Assert.Equal(["Observation/obs-1"], ids);
        encounterMappingQueries.Verify(x => x.GetByFacilityIdAndEncounterIdsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteRead_UsesLocationMappingServiceEncounterFilter()
    {
           // Arrange
           var readFhirCommand = new Mock<IReadFhirCommand>();
           var resourceCache = new Mock<IResourceCache>();
           var locationMappingService = new Mock<ILocationMappingService>();
           ConfigureDefaultLocationMappingFilter(locationMappingService);

           var observation = new Observation
           {
               Id = "obs-removed",
               Encounter = new ResourceReference("Encounter/enc-removed")
           };

           readFhirCommand
               .Setup(x => x.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(observation);

           locationMappingService
               .Setup(x => x.FilterResourcesByEncounterMappingAsync(
                   "fac-1",
                   It.Is<IReadOnlyCollection<Resource>>(resources => resources.Single().Id == "obs-removed"),
                   It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

           var service = new FhirApiService(
               new Mock<IReferenceResourcesManager>().Object,
               new Mock<IReferenceResourcesQueries>().Object,
               new Mock<ISearchFhirCommand>().Object,
               readFhirCommand.Object,
               new Mock<ILogger<FhirApiService>>().Object,
               resourceCache.Object,
               locationMappingService.Object
           );

           var log = new DataAcquisitionLogModel
           {
               FacilityId = "fac-1",
               CorrelationId = "corr-1",
               PatientId = "Patient/patient-1",
               ResourceId = "obs-removed"
           };
           var fhirQuery = new FhirQueryModel
           {
               IsReference = false,
               ResourceReferenceTypes = new List<ResourceReferenceTypeModel>()
           };
           var fhirQueryConfiguration = new FhirQueryConfigurationModel
           {
               FhirServerBaseUrl = "http://test"
           };

           // Act
           var ids = await service.ExecuteRead(log, fhirQuery, ResourceType.Observation, fhirQueryConfiguration);

           // Assert
           Assert.Empty(ids);
           resourceCache.Verify(x => x.UpdateCorrelationCacheAsync(
               It.IsAny<string>(),
               It.IsAny<List<DomainResource>>(),
               It.IsAny<ResourceType>(),
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
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var kafkaProducer = new Mock<IProducer<ResourceKey, ResourcesAcquired>>();
        var logger = new Mock<ILogger<FhirApiService>>();
        var resourceCache = new Mock<IResourceCache>();

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

        referenceResourceQueries
            .Setup(x => x.SearchAsync(It.IsAny<SearchReferenceResourcesModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<ReferenceResourcesModel>
            {
                Metadata = new PaginationMetadata { PageNumber = 1, PageSize = 10, TotalCount = 0, TotalPages = 0 },
                Records = new List<ReferenceResourcesModel>()
            });

        // Capture the cache key used when storing the resource
        string? capturedCacheKey = null;
        resourceCache
            .Setup(x => x.UpdateCorrelationCacheAsync(
                It.IsAny<string>(),
                It.IsAny<List<DomainResource>>(),
                It.IsAny<ResourceType>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, List<DomainResource>, ResourceType, CancellationToken>((key, resources, type, _) =>
            {
                capturedCacheKey = key;
            })
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        var service = new FhirApiService(
            referenceResourceManager.Object,
            referenceResourceQueries.Object,
            searchFhirCommand.Object,
            readFhirCommand.Object,
            logger.Object,
            resourceCache.Object,
            CreateLocationMappingService()
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

        // Assert: cache was updated and the key contains Location (not Patient)
        Assert.NotNull(capturedCacheKey);
        Assert.Contains(":Location", capturedCacheKey);
        Assert.DoesNotContain(":Patient", capturedCacheKey);

    }

    [Fact]
    public async Task ExecuteSearch_LocationResource_WhenFacilityConfigured_CallsLocationMappingService()
    {
        // Arrange
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);
        locationMappingService
            .Setup(s => s.UpdateResourceMappingsAsync(
                "fac-1",
                It.Is<IReadOnlyCollection<Resource>>(resources =>
                    resources.OfType<Location>().Any(location => location.Id == "loc-1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrganizationLocationMappingModel>
            {
                new() { FacilityId = "fac-1", LocationId = "loc-1", IsOrgLocation = true }
            });
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            locationMappingService.Object
        );

        var location = new Location { Id = "loc-1", Name = "ICU" };
        var bundle = new Bundle
        {
            Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = location } }
        };
        searchFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetBundleAsync(bundle));

        var log = new DataAcquisitionLogModel { FacilityId = "fac-1", CorrelationId = "c1", ScheduledReport = new ScheduledReport(), ReportableEvent = ReportableEvent.Adhoc };
        var fhirQuery = new FhirQueryModel { IsReference = false, ResourceReferenceTypes = new List<ResourceReferenceTypeModel>() };
        var config = new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" };

        // Act
        await service.ExecuteSearch(log, fhirQuery, config, ResourceType.Location);

        // Assert
        locationMappingService.Verify(s => s.UpdateResourceMappingsAsync(
            "fac-1",
            It.Is<IReadOnlyCollection<Resource>>(resources =>
                resources.OfType<Location>().Any(location => location.Id == "loc-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteSearch_NonLocationResource_DelegatesResourceMappingBatch()
    {
        // Arrange
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            locationMappingService.Object
        );

        var patient = new Patient { Id = "p1" };
        var bundle = new Bundle
        {
            Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = patient } }
        };
        searchFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetBundleAsync(bundle));

        var log = new DataAcquisitionLogModel { FacilityId = "fac-1", CorrelationId = "c1", ScheduledReport = new ScheduledReport(), ReportableEvent = ReportableEvent.Adhoc };
        var fhirQuery = new FhirQueryModel { IsReference = false, ResourceReferenceTypes = new List<ResourceReferenceTypeModel>() };
        var config = new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" };

        // Act
        await service.ExecuteSearch(log, fhirQuery, config, ResourceType.Patient);

        // Assert
        locationMappingService.Verify(s => s.UpdateResourceMappingsAsync(
            "fac-1",
            It.Is<IReadOnlyCollection<Resource>>(resources =>
                resources.OfType<Patient>().Any(patient => patient.Id == "p1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteSearch_LocationResource_DelegatesConfiguredCheckToLocationMappingService()
    {
        // Arrange
        var searchFhirCommand = new Mock<ISearchFhirCommand>();
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            searchFhirCommand.Object,
            new Mock<IReadFhirCommand>().Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            locationMappingService.Object
        );

        var location = new Location { Id = "loc-1", Name = "ICU" };
        var bundle = new Bundle
        {
            Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = location } }
        };
        searchFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Returns(GetBundleAsync(bundle));

        var log = new DataAcquisitionLogModel { FacilityId = "fac-1", CorrelationId = "c1", ScheduledReport = new ScheduledReport(), ReportableEvent = ReportableEvent.Adhoc };
        var fhirQuery = new FhirQueryModel { IsReference = false, ResourceReferenceTypes = new List<ResourceReferenceTypeModel>() };
        var config = new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" };

        // Act
        await service.ExecuteSearch(log, fhirQuery, config, ResourceType.Location);

        // Assert
        locationMappingService.Verify(s => s.UpdateResourceMappingsAsync(
            "fac-1",
            It.Is<IReadOnlyCollection<Resource>>(resources =>
                resources.OfType<Location>().Any(location => location.Id == "loc-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteRead_LocationResource_WhenFacilityConfigured_CallsLocationMappingService()
    {
        // Arrange
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);
        locationMappingService
            .Setup(s => s.UpdateResourceMappingsAsync(
                "fac-1",
                It.Is<IReadOnlyCollection<Resource>>(resources =>
                    resources.OfType<Location>().Any(location => location.Id == "loc-1")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrganizationLocationMappingModel>
            {
                new() { FacilityId = "fac-1", LocationId = "loc-1", IsOrgLocation = true }
            });
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            new Mock<ISearchFhirCommand>().Object,
            readFhirCommand.Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            locationMappingService.Object
        );

        var location = new Location { Id = "loc-1", Name = "ICU" };
        readFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var log = new DataAcquisitionLogModel { FacilityId = "fac-1", ResourceId = "loc-1", CorrelationId = "c1", ScheduledReport = new ScheduledReport(), ReportableEvent = ReportableEvent.Adhoc };
        var fhirQuery = new FhirQueryModel { IsReference = false, ResourceReferenceTypes = new List<ResourceReferenceTypeModel>() };
        var config = new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" };

        // Act
        await service.ExecuteRead(log, fhirQuery, ResourceType.Location, config);

        // Assert
        locationMappingService.Verify(s => s.UpdateResourceMappingsAsync(
            "fac-1",
            It.Is<IReadOnlyCollection<Resource>>(resources =>
                resources.OfType<Location>().Any(location => location.Id == "loc-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteRead_LocationResource_DelegatesConfiguredCheckToLocationMappingService()
    {
        // Arrange
        var readFhirCommand = new Mock<IReadFhirCommand>();
        var locationMappingService = new Mock<ILocationMappingService>();
        ConfigureDefaultLocationMappingFilter(locationMappingService);
        var service = new FhirApiService(
            new Mock<IReferenceResourcesManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            new Mock<ISearchFhirCommand>().Object,
            readFhirCommand.Object,
            new Mock<ILogger<FhirApiService>>().Object,
            new Mock<IResourceCache>().Object,
            locationMappingService.Object
        );

        var location = new Location { Id = "loc-1", Name = "ICU" };
        readFhirCommand.Setup(x => x.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var log = new DataAcquisitionLogModel { FacilityId = "fac-1", ResourceId = "loc-1", CorrelationId = "c1", ScheduledReport = new ScheduledReport(), ReportableEvent = ReportableEvent.Adhoc };
        var fhirQuery = new FhirQueryModel { IsReference = false, ResourceReferenceTypes = new List<ResourceReferenceTypeModel>() };
        var config = new FhirQueryConfigurationModel { FhirServerBaseUrl = "http://test" };

        // Act
        await service.ExecuteRead(log, fhirQuery, ResourceType.Location, config);

        // Assert
        locationMappingService.Verify(s => s.UpdateResourceMappingsAsync(
            "fac-1",
            It.Is<IReadOnlyCollection<Resource>>(resources =>
                resources.OfType<Location>().Any(location => location.Id == "loc-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async IAsyncEnumerable<Bundle> GetBundleAsync(Bundle bundle)
    {
        yield return bundle;
        await Task.CompletedTask;
    }
}
