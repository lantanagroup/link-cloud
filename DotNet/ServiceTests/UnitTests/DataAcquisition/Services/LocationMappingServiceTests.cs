using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using System.Text.Json;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.UnitTests.DataAcquisition.Services;

[Trait("Category", "UnitTests")]
public class LocationMappingServiceTests
{
    private const string FacilityId = "facility-1";
    private const int NewMappingId = 42;

    private readonly Mock<IOrganizationLocationMappingManager> _mockManager = new();
    private readonly Mock<IOrganizationLocationMappingQueries> _mockQueries = new();
    private readonly Mock<IOrganizationLocationConfigurationQueries> _mockConfigQueries = new();
    private readonly Mock<IEncounterMappingQueries> _mockEncounterMappingQueries = new();
    private readonly Mock<IEncounterMappingManager> _mockEncounterMappingManager = new();
    private readonly Mock<IReferenceResourcesQueries> _mockReferenceResourcesQueries = new();
    private readonly Mock<ICacheService> _mockCache = new();
    private readonly Mock<IResourceCache> _mockResourceCache = new();
    private readonly Mock<ILogger<LocationMappingService>> _mockLogger = new();

    // Stateful conditions-cache backing field. Seeded with a non-matching condition so the facility
    // reads as "configured" (non-empty) while generic test locations are not org locations.
    private List<OrganizationLocationConditionModel>? _cachedConditions =
    [
        new() { ConditionId = 99, Priority = 1, FhirPath = "Location.name = 'ORG-THAT-DOES-NOT-MATCH'" }
    ];

    private readonly LocationMappingService _service;

    public LocationMappingServiceTests()
    {
        // Default: no existing mapping for any (facility, location) lookup.
        _mockQueries
            .Setup(q => q.GetByFacilityIdAndLocationIdAsync(It.IsAny<string>(), It.IsAny<string>()))!
            .ReturnsAsync((OrganizationLocationMappingModel?)null);

        // Default: CreateAsync echoes the inbound model back with a generated id.
        _mockManager
            .Setup(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationMappingModel>()))
            .ReturnsAsync((CreateOrganizationLocationMappingModel m) => new OrganizationLocationMappingModel
            {
                LocationMappingId = NewMappingId,
                FacilityId = m.FacilityId,
                LocationId = m.LocationId,
                LocationName = m.LocationName,
                LocationAlias = m.LocationAlias,
                PartOfValue = m.PartOfValue,
                PartOfId = m.PartOfId,
                IsOrgLocation = m.IsOrgLocation,
                IsActive = m.IsActive
            });

        // Default: UpdateByIdAsync echoes the update back.
        _mockManager
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()))
            .ReturnsAsync((int id, UpdateOrganizationLocationMappingModel m) => new OrganizationLocationMappingModel
            {
                LocationMappingId = id,
                FacilityId = FacilityId,
                LocationName = m.LocationName,
                LocationAlias = m.LocationAlias,
                PartOfValue = m.PartOfValue,
                PartOfId = m.PartOfId,
                IsOrgLocation = m.IsOrgLocation ?? false,
                IsActive = m.IsActive ?? true
            });

        _mockManager
            .Setup(m => m.SetParentForChildrenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Stateful cache: Get returns the last Set value (seeded above with a non-matching
        // condition so the facility is "configured" but generic locations are not org locations).
        _mockCache
            .Setup(c => c.Get<List<OrganizationLocationConditionModel>?>(It.IsAny<string>()))
            .Returns(() => _cachedConditions);
        _mockCache
            .Setup(c => c.Set(It.IsAny<string>(), It.IsAny<List<OrganizationLocationConditionModel>>(),
                It.IsAny<TimeSpan>(), It.IsAny<ExpirationType>()))
            .Callback<string, List<OrganizationLocationConditionModel>, TimeSpan, ExpirationType>(
                (_, value, _, _) => _cachedConditions = value);

        _mockConfigQueries
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<OrganizationLocationConfigurationModel>());

        _service = new LocationMappingService(
            _mockManager.Object,
            _mockQueries.Object,
            _mockConfigQueries.Object,
            _mockEncounterMappingQueries.Object,
            _mockEncounterMappingManager.Object,
            _mockReferenceResourcesQueries.Object,
            _mockCache.Object,
            _mockResourceCache.Object,
            _mockLogger.Object);
    }

    private static Location NewLocation(
        string id,
        string? name = null,
        string? partOfReference = null,
        params string[] aliases)
    {
        var location = new Location { Id = id, Name = name };
        if (partOfReference != null)
        {
            location.PartOf = new ResourceReference(partOfReference);
        }

        if (aliases.Length > 0)
        {
            location.Alias = aliases.ToList();
        }

        return location;
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenNoExistingMapping_CreatesNewMapping()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "ICU");

        // Act
        var result = await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.FacilityId == FacilityId &&
            c.LocationId == "loc-1" &&
            c.LocationName == "ICU" &&
            c.IsActive &&
            !c.IsOrgLocation)), Times.Once);
        _mockManager.Verify(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()),
            Times.Never);
        Assert.Equal(NewMappingId, result.LocationMappingId);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_NormalizesLocationIdAndPartOfToBareIds()
    {
        // Arrange
        // PartOf is a type-prefixed FHIR reference; LocationId/PartOfValue must be stored bare.
        var location = NewLocation("ed-1", name: "ED", partOfReference: "Location/hosp-1");

        // Parent already mapped → PartOfId should be resolved from it.
        _mockQueries
            .Setup(q => q.GetByFacilityIdAndLocationIdAsync(FacilityId, "hosp-1"))
            .ReturnsAsync(new OrganizationLocationMappingModel { LocationMappingId = 5, LocationId = "hosp-1" });

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        // "Location/" stripped
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.LocationId == "ed-1" &&
            c.PartOfValue == "hosp-1" && 
            c.PartOfId == 5)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenParentNotYetMapped_LeavesPartOfIdNull()
    {
        // Arrange
        var location = NewLocation("ed-1", name: "ED", partOfReference: "Location/hosp-1");
        // Parent lookup returns null (default) → PartOfId unresolved, PartOfValue still recorded.

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.PartOfValue == "hosp-1" &&
            c.PartOfId == null)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenParentIsOrgLocation_AppliesParentValueToChild()
    {
        // Arrange
        var location = NewLocation("ed-1", name: "ED", partOfReference: "Location/hosp-1");
        _mockQueries
            .Setup(q => q.GetByFacilityIdAndLocationIdAsync(FacilityId, "hosp-1"))
            .ReturnsAsync(new OrganizationLocationMappingModel
            {
                LocationMappingId = 5,
                LocationId = "hosp-1",
                IsOrgLocation = true
            });

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.LocationId == "ed-1" &&
            c.PartOfId == 5 &&
            c.IsOrgLocation)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_AfterAdd_RunsChildBackfillWithOwnLocationId()
    {
        // Arrange
        var location = NewLocation("hosp-1", name: "Hospital");

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        // Adopt orphans whose PartOfValue points at THIS location's id, under the new mapping id.
        _mockManager.Verify(m => m.SetParentForChildrenAsync(
            FacilityId, "hosp-1", NewMappingId, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_PrefersProvidedAlias_OverFhirAlias()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "ICU", aliases: "FHIR-ALIAS");

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location, locationAlias: "OVERRIDE");

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.LocationAlias == "OVERRIDE")), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_FallsBackToFhirAlias_WhenNoOverride()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "ICU", aliases: "FHIR-ALIAS");

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.LocationAlias == "FHIR-ALIAS")), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenExistingMappingUnchanged_DoesNotUpdate()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "ICU");
        _mockQueries
            .Setup(q => q.GetByFacilityIdAndLocationIdAsync(FacilityId, "loc-1"))
            .ReturnsAsync(new OrganizationLocationMappingModel
            {
                LocationMappingId = 7,
                FacilityId = FacilityId,
                LocationId = "loc-1",
                LocationName = "ICU",
                LocationAlias = null,
                PartOfValue = null,
                PartOfId = null,
                IsOrgLocation = false
            });

        // Act
        var result = await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()),
            Times.Never);
        _mockManager.Verify(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationMappingModel>()), Times.Never);
        Assert.Equal(7, result.LocationMappingId);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenExistingMappingChanged_UpdatesWithNewValues()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "ICU - Renamed");
        _mockQueries
            .Setup(q => q.GetByFacilityIdAndLocationIdAsync(FacilityId, "loc-1"))
            .ReturnsAsync(new OrganizationLocationMappingModel
            {
                LocationMappingId = 7,
                FacilityId = FacilityId,
                LocationId = "loc-1",
                LocationName = "ICU", // old name differs → triggers update
                IsOrgLocation = false
            });

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.UpdateByIdAsync(7, It.Is<UpdateOrganizationLocationMappingModel>(u =>
            u.LocationName == "ICU - Renamed")), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenExistingPlaceholderHydrated_BackfillsWaitingChildren()
    {
        // Arrange
        var location = NewLocation("parent-1", name: "Hospital", aliases: "HOSP");
        _mockQueries
            .Setup(q => q.GetByFacilityIdAndLocationIdAsync(FacilityId, "parent-1"))
            .ReturnsAsync(new OrganizationLocationMappingModel
            {
                LocationMappingId = 7,
                FacilityId = FacilityId,
                LocationId = "parent-1",
                LocationName = null,
                LocationAlias = null,
                PartOfValue = null,
                PartOfId = null,
                IsOrgLocation = false,
                IsActive = true
            });

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location, cancellationToken: CancellationToken.None);

        // Assert
        _mockManager.Verify(m => m.UpdateByIdAsync(7, It.Is<UpdateOrganizationLocationMappingModel>(u =>
            u.LocationName == "Hospital" &&
            u.LocationAlias == "HOSP" &&
            u.PartOfValue == null &&
            u.PartOfId == null)), Times.Once);
        _mockManager.Verify(m => m.SetParentForChildrenAsync(
            FacilityId, "parent-1", 7, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReevaluateLocationMappingsAsync_WhenNoMappingsExist_HydratesCachedLocations()
    {
        // Arrange
        _mockQueries
            .Setup(q => q.GetByFacilityIdAsync(FacilityId))
            .ReturnsAsync(new List<OrganizationLocationMappingModel>());

        var location = NewLocation("loc-cached", name: "Cached Unit");
        var cachedLocation = new ReferenceResourcesModel
        {
            Id = Guid.NewGuid(),
            FacilityId = FacilityId,
            ResourceId = location.Id,
            ResourceType = ResourceType.Location.ToString(),
            QueryPhase = QueryPhase.Initial,
            ReferenceResource = JsonSerializer.Serialize<Resource>(
                location,
                LinkFhirSerializerOptions.ForFhirLenientSerialization)
        };

        _mockReferenceResourcesQueries
            .Setup(q => q.SearchAsync(
                It.Is<SearchReferenceResourcesModel>(s =>
                    s.FacilityId == FacilityId &&
                    s.ResourceType == ResourceType.Location.ToString() &&
                    s.PageSize == int.MaxValue &&
                    (s.ResourceIds == null || s.ResourceIds.Count == 0)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<ReferenceResourcesModel>
            {
                Records = [cachedLocation],
                Metadata = new PaginationMetadata { PageNumber = 1, PageSize = 1, TotalCount = 1, TotalPages = 1 }
            });

        // Act
        await _service.ReevaluateLocationMappingsAsync(FacilityId, CancellationToken.None);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.FacilityId == FacilityId &&
            c.LocationId == "loc-cached" &&
            c.LocationName == "Cached Unit")), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenLocationMatchesOrgCondition_MarksAsOrgLocation()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "University Hospital");
        _cachedConditions =
        [
            new() { ConditionId = 1, Priority = 1, FhirPath = "Location.name = 'University Hospital'" }
        ];

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.IsOrgLocation)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenLocationDoesNotMatchOrgCondition_NotOrgLocation()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "Some Ward");
        _cachedConditions =
        [
            new() { ConditionId = 1, Priority = 1, FhirPath = "Location.name = 'University Hospital'" }
        ];

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            !c.IsOrgLocation)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenAnyConditionMatches_MarksAsOrgLocation()
    {
        // Arrange
        // OR semantics: one matching condition is enough; evaluation short-circuits on the first
        // match, so a malformed sibling condition is never reached.
        var location = NewLocation("loc-1", name: "University Hospital");
        _cachedConditions =
        [
            new() { ConditionId = 1, Priority = 1, FhirPath = "Location.name = 'University Hospital'" },
            new() { ConditionId = 2, Priority = 2, FhirPath = "this.is.not.valid.fhirpath(" }
        ];

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.IsOrgLocation)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenConditionsNotCached_LoadsActiveConditionsAndCaches()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "University Hospital");

        // Cache miss → must load config and re-cache.
        _cachedConditions = null;

        _mockConfigQueries
            .Setup(q => q.GetByFacilityIdAsync(FacilityId))
            .ReturnsAsync(new List<OrganizationLocationConfigurationModel>
            {
                new()
                {
                    FacilityId = FacilityId,
                    IsActive = true,
                    Conditions = new List<OrganizationLocationConditionModel>
                    {
                        new() { ConditionId = 1, Priority = 1, FhirPath = "Location.name = 'University Hospital'" }
                    }
                },
                new()
                {
                    FacilityId = FacilityId,
                    IsActive = false, // inactive config must be ignored
                    Conditions = new List<OrganizationLocationConditionModel>
                    {
                        new() { ConditionId = 2, Priority = 1, FhirPath = "Location.name = 'Something Else'" }
                    }
                }
            });

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockConfigQueries.Verify(q => q.GetByFacilityIdAsync(FacilityId), Times.Once);
        _mockCache.Verify(c => c.Set(
            It.IsAny<string>(),
            It.Is<List<OrganizationLocationConditionModel>>(l => l.Count == 1 && l[0].ConditionId == 1),
            It.IsAny<TimeSpan>(),
            ExpirationType.Absolute), Times.Once);
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.IsOrgLocation)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenCreateThrowsNonUniqueDbUpdateException_Propagates()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "ICU");
        _mockManager
            .Setup(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationMappingModel>()))
            .ThrowsAsync(new DbUpdateException("boom", new InvalidOperationException("not a unique violation")));

        // Act & Assert
        // The when-filter only catches unique-constraint violations; anything else must surface.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _service.UpdateLocationMappingAsync(FacilityId, location));

        _mockManager.Verify(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()),
            Times.Never);
        _mockManager.Verify(m => m.SetParentForChildrenAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsConfigured_WhenFacilityHasActiveConditions_ReturnsTrue()
    {
        // Arrange — the default cache is seeded with one active condition.

        // Act
        var configured = await _service.IsConfigured(FacilityId, CancellationToken.None);

        // Assert
        Assert.True(configured);
    }

    [Fact]
    public async Task IsConfigured_WhenFacilityHasNoActiveConditions_ReturnsFalse()
    {
        // Arrange
        _cachedConditions = [];

        // Act
        var configured = await _service.IsConfigured(FacilityId, CancellationToken.None);

        // Assert
        Assert.False(configured);
    }

    [Fact]
    public async Task UpdateResourceMappingsAsync_WhenConfigured_UpdatesLocationResources()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "ICU");
        var patient = new Patient { Id = "patient-1" };

        // Act
        var results = await _service.UpdateResourceMappingsAsync(
            FacilityId,
            [location, patient],
            CancellationToken.None);

        // Assert
        var result = Assert.Single(results);
        Assert.Equal("loc-1", result.LocationId);
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            c.FacilityId == FacilityId &&
            c.LocationId == "loc-1" &&
            c.LocationName == "ICU")), Times.Once);
    }

    [Fact]
    public async Task UpdateResourceMappingsAsync_WhenNotConfigured_NoOps()
    {
        // Arrange
        _cachedConditions = [];
        var location = NewLocation("loc-1", name: "ICU");

        // Act
        var results = await _service.UpdateResourceMappingsAsync(
            FacilityId,
            [location],
            CancellationToken.None);

        // Assert
        Assert.Empty(results);
        _mockManager.Verify(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationMappingModel>()), Times.Never);
        _mockEncounterMappingManager.Verify(
            m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()),
            Times.Never);
    }

    [Fact]
    public async Task FilterResourcesByEncounterMappingAsync_WhenConfigured_RemovesResourcesWithoutMappedEncounter()
    {
        // Arrange
        var mappedObservation = new Observation
        {
            Id = "obs-mapped",
            Encounter = new ResourceReference("Encounter/enc-mapped")
        };
        var unmappedObservation = new Observation
        {
            Id = "obs-unmapped",
            Encounter = new ResourceReference("Encounter/enc-unmapped")
        };
        var noEncounterObservation = new Observation { Id = "obs-no-encounter" };

        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockEncounterMappingQueries
            .Setup(x => x.GetByFacilityIdAndEncounterIdsAsync(
                FacilityId,
                It.Is<IReadOnlyCollection<string>>(ids =>
                    ids.Contains("enc-mapped") &&
                    ids.Contains("enc-unmapped")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new()
                {
                    FacilityId = FacilityId,
                    PatientId = "patient-1",
                    EncounterId = "enc-mapped",
                    MappedToOrg = true
                },
                new()
                {
                    FacilityId = FacilityId,
                    PatientId = "patient-1",
                    EncounterId = "enc-unmapped",
                    MappedToOrg = false
                }
            });

        // Act
        var filtered = await _service.FilterResourcesByEncounterMappingAsync(
            FacilityId,
            [mappedObservation, unmappedObservation, noEncounterObservation],
            CancellationToken.None);

        // Assert
        Assert.Equal(["obs-mapped", "obs-no-encounter"], filtered.Select(resource => resource.Id));
    }

    [Fact]
    public async Task FilterResourcesByEncounterMappingAsync_WhenNotConfigured_DoesNotQueryEncounterMappings()
    {
        // Arrange
        var observation = new Observation
        {
            Id = "obs-1",
            Encounter = new ResourceReference("Encounter/enc-1")
        };

        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var filtered = await _service.FilterResourcesByEncounterMappingAsync(
            FacilityId,
            [observation],
            CancellationToken.None);

        // Assert
        Assert.Equal(["obs-1"], filtered.Select(resource => resource.Id));
        _mockEncounterMappingQueries.Verify(x => x.GetByFacilityIdAndEncounterIdsAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FilterResourcesByEncounterMappingAsync_WhenResourceEncounterIsUnmapped_RemovesResource()
    {
        // Arrange
        var observation = new Observation
        {
            Id = "obs-unmapped",
            Encounter = new ResourceReference("Encounter/enc-unmapped")
        };

        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockEncounterMappingQueries
            .Setup(x => x.GetByFacilityIdAndEncounterIdsAsync(
                FacilityId,
                It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "enc-unmapped" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new()
                {
                    FacilityId = FacilityId,
                    PatientId = "patient-1",
                    EncounterId = "enc-unmapped",
                    MappedToOrg = false
                }
            });

        // Act
        var filtered = await _service.FilterResourcesByEncounterMappingAsync(
            FacilityId,
            [observation],
            CancellationToken.None);

        // Assert
        Assert.Empty(filtered);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_WhenFacilityNotConfigured_ThrowsNotFound()
    {
        // Arrange — no active conditions → the facility is not configured for location mapping.
        _cachedConditions = [];
        var location = NewLocation("loc-1", name: "ICU");

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateLocationMappingAsync(FacilityId, location));

        _mockManager.Verify(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationMappingModel>()), Times.Never);
        _mockManager.Verify(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()),
            Times.Never);
    }

    [Fact]
    public async Task IsPatientReportableAsync_WhenNotConfigured_ReturnsTrueWithoutQueryingEncounterMappings()
    {
        // Arrange — org-location mapping not active for the facility.
        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var reportable = await _service.IsPatientReportableAsync(FacilityId, "patient-1", CancellationToken.None);

        // Assert — reportable by default, and we never look at encounter mappings.
        Assert.True(reportable);
        _mockEncounterMappingQueries.Verify(x => x.GetByFacilityIdAndPatientIdAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsPatientReportableAsync_WhenNoEncounterMappings_ReturnsTrueFailOpen()
    {
        // Arrange — configured, but the patient has no encounter mappings recorded yet.
        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockEncounterMappingQueries
            .Setup(x => x.GetByFacilityIdAndPatientIdAsync(FacilityId, "patient-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>());

        // Act
        var reportable = await _service.IsPatientReportableAsync(FacilityId, "patient-1", CancellationToken.None);

        // Assert — fail open: cannot conclude non-reportable, so keep acquiring.
        Assert.True(reportable);
    }

    [Fact]
    public async Task IsPatientReportableAsync_WhenAtLeastOneEncounterMappedToOrg_ReturnsTrue()
    {
        // Arrange — configured; one of the patient's encounters maps to the org.
        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockEncounterMappingQueries
            .Setup(x => x.GetByFacilityIdAndPatientIdAsync(FacilityId, "patient-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new() { FacilityId = FacilityId, PatientId = "patient-1", EncounterId = "enc-1", MappedToOrg = false },
                new() { FacilityId = FacilityId, PatientId = "patient-1", EncounterId = "enc-2", MappedToOrg = true }
            });

        // Act
        var reportable = await _service.IsPatientReportableAsync(FacilityId, "patient-1", CancellationToken.None);

        // Assert
        Assert.True(reportable);
    }

    [Fact]
    public async Task IsPatientReportableAsync_WhenNoEncounterMappedToOrg_ReturnsFalse()
    {
        // Arrange — configured; the patient has encounter mappings but none map to the org.
        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockEncounterMappingQueries
            .Setup(x => x.GetByFacilityIdAndPatientIdAsync(FacilityId, "patient-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new() { FacilityId = FacilityId, PatientId = "patient-1", EncounterId = "enc-1", MappedToOrg = false },
                new() { FacilityId = FacilityId, PatientId = "patient-1", EncounterId = "enc-2", MappedToOrg = false }
            });

        // Act
        var reportable = await _service.IsPatientReportableAsync(FacilityId, "patient-1", CancellationToken.None);

        // Assert — every encounter is non-org → patient is not reportable.
        Assert.False(reportable);
    }

    [Fact]
    public async Task IsPatientReportableAsync_WhenCurrentReportEncountersAreNonOrg_IgnoresHistoricalOrgEncounter()
    {
        // Arrange — configured; the patient has a historical org encounter, but the current report
        // only acquired a non-org encounter.
        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockEncounterMappingQueries
            .Setup(x => x.GetByFacilityIdAndEncounterIdsAsync(
                FacilityId,
                It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "enc-current" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new() { FacilityId = FacilityId, PatientId = "patient-1", EncounterId = "enc-current", MappedToOrg = false }
            });

        // Act
        var reportable = await _service.IsPatientReportableAsync(
            FacilityId,
            "patient-1",
            ["Encounter/enc-current"],
            CancellationToken.None);

        // Assert — scoped reportability is false even though an unscoped historical lookup would
        // have found an org encounter for the same patient.
        Assert.False(reportable);
        _mockEncounterMappingQueries.Verify(x => x.GetByFacilityIdAndPatientIdAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsPatientReportableAsync_WhenCurrentReportEncounterIdsAreEmpty_ReturnsTrueFailOpen()
    {
        // Arrange — configured, but no current-report Encounter ids were found.
        _mockConfigQueries
            .Setup(x => x.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var reportable = await _service.IsPatientReportableAsync(
            FacilityId,
            "patient-1",
            Array.Empty<string>(),
            CancellationToken.None);

        // Assert — fail open for this report; do not fall back to all historical patient encounters.
        Assert.True(reportable);
        _mockEncounterMappingQueries.Verify(x => x.GetByFacilityIdAndPatientIdAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockEncounterMappingQueries.Verify(x => x.GetByFacilityIdAndEncounterIdsAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEncounterLocationMappingAsync_AnyReferencedLocationIsOrg_SetsMappedToOrgTrue()
    {
        // Arrange — the encounter references two locations: the first IS an org location, the
        // second is not. "Mapped to org" should be true (any org location qualifies), independent
        // of iteration order. With the location order [org, non-org] the value must not be
        // overwritten by the trailing non-org location.
        const string orgLocationId = "loc-org";
        const string nonOrgLocationId = "loc-nonorg";

        _mockQueries
            .Setup(q => q.GetByFacilityIdAsync(FacilityId))
            .ReturnsAsync(new List<OrganizationLocationMappingModel>
            {
                new() { LocationMappingId = 1, FacilityId = FacilityId, LocationId = orgLocationId, IsOrgLocation = true },
                new() { LocationMappingId = 2, FacilityId = FacilityId, LocationId = nonOrgLocationId, IsOrgLocation = false }
            });

        CreateEncounterMappingModel? captured = null;
        _mockEncounterMappingManager
            .Setup(m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()))
            .Callback<CreateEncounterMappingModel>(m => captured = m)
            .ReturnsAsync((CreateEncounterMappingModel m) => new EncounterMappingModel
            {
                EncounterMappingId = 1,
                FacilityId = m.FacilityId,
                PatientId = m.PatientId,
                EncounterId = m.EncounterId,
                MappedToOrg = m.MappedToOrg
            });

        var encounter = new Encounter
        {
            Id = "enc-1",
            Subject = new ResourceReference("Patient/patient-1"),
            Location = new List<Encounter.LocationComponent>
            {
                new() { Location = new ResourceReference($"Location/{orgLocationId}") },
                new() { Location = new ResourceReference($"Location/{nonOrgLocationId}") }
            }
        };

        // Act
        await _service.UpdateEncounterLocationMappingAsync(FacilityId, encounter, CancellationToken.None);

        // Assert — at least one referenced location is an org location → mapped to org.
        Assert.NotNull(captured);
        Assert.True(captured!.MappedToOrg);
    }

    [Fact]
    public async Task StripNonOrgEncountersFromCacheAsync_RemovesNonOrgEncounters_KeepsOrgEncounters()
    {
        // Arrange — a mixed patient: one org encounter, one non-org.
        const string correlationId = "corr-1";
        const string patientId = "patient-1";
        var cacheKey = $"{correlationId}:{ResourceType.Encounter}";
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockConfigQueries
            .Setup(q => q.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockResourceCache
            .Setup(c => c.GetAsync(cacheKey, cancellationToken))
            .ReturnsAsync(new List<DomainResource>
            {
                new Encounter { Id = "enc-org" },
                new Encounter { Id = "enc-nonorg" }
            });

        _mockEncounterMappingQueries
            .Setup(q => q.GetByFacilityIdAndPatientIdAsync(FacilityId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new() { EncounterId = "enc-org", MappedToOrg = true },
                new() { EncounterId = "enc-nonorg", MappedToOrg = false }
            });

        List<DomainResource>? rewritten = null;
        _mockResourceCache
            .Setup(c => c.UpdateCorrelationCacheAsync(cacheKey, It.IsAny<List<DomainResource>>(), ResourceType.Encounter, cancellationToken))
            .Callback<string, List<DomainResource>, ResourceType, CancellationToken>((_, resources, _, _) => rewritten = resources)
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        // Act
        var stripped = await _service.StripNonOrgEncountersFromCacheAsync(
            FacilityId, correlationId, patientId, cancellationToken);

        // Assert — only the non-org encounter is removed; the org encounter is rewritten.
        Assert.Equal(1, stripped);
        _mockResourceCache.Verify(c => c.DeleteAsync(It.Is<List<string>>(keys => keys.Contains(cacheKey)), cancellationToken), Times.Once);
        Assert.NotNull(rewritten);
        Assert.Single(rewritten!);
        Assert.Equal("enc-org", rewritten![0].Id);
    }

    [Fact]
    public async Task StripNonOrgEncountersFromCacheAsync_AllEncountersNonOrg_LeavesCacheEmpty()
    {
        // Arrange — a fully non-reportable patient: the only encounter is non-org.
        const string correlationId = "corr-2";
        const string patientId = "patient-2";
        var cacheKey = $"{correlationId}:{ResourceType.Encounter}";

        _mockConfigQueries
            .Setup(q => q.HasActiveByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockResourceCache
            .Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainResource> { new Encounter { Id = "enc-nonorg" } });
        _mockEncounterMappingQueries
            .Setup(q => q.GetByFacilityIdAndPatientIdAsync(FacilityId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel> { new() { EncounterId = "enc-nonorg", MappedToOrg = false } });

        // Act
        var stripped = await _service.StripNonOrgEncountersFromCacheAsync(
            FacilityId, correlationId, patientId, CancellationToken.None);

        // Assert — the key is deleted and never rewritten, so MeasureEval rehydrates no qualifying encounter.
        Assert.Equal(1, stripped);
        _mockResourceCache.Verify(c => c.DeleteAsync(It.Is<List<string>>(keys => keys.Contains(cacheKey)), It.IsAny<CancellationToken>()), Times.Once);
        _mockResourceCache.Verify(
            c => c.UpdateCorrelationCacheAsync(It.IsAny<string>(), It.IsAny<List<DomainResource>>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StripNonOrgEncountersFromCacheAsync_FacilityNotConfigured_NoOp()
    {
        // Arrange — org-location mapping is inactive for the facility.
        _mockConfigQueries
            .Setup(q => q.HasActiveByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var stripped = await _service.StripNonOrgEncountersFromCacheAsync(
            FacilityId, "corr-3", "patient-3", CancellationToken.None);

        // Assert — never touches the resource cache or encounter mappings when mapping is inactive.
        Assert.Equal(0, stripped);
        _mockResourceCache.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockResourceCache.Verify(c => c.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockEncounterMappingQueries.Verify(
            q => q.GetByFacilityIdAndPatientIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
