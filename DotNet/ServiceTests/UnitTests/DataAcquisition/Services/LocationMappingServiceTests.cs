using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
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
    private readonly Mock<ICacheService> _mockCache = new();
    private readonly Mock<ILogger<LocationMappingService>> _mockLogger = new();

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
            .Setup(m => m.SetPartOfIdForChildrenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Default: conditions cache hit with an empty list → IsOrgLocation evaluates to false
        // without touching the config queries.
        _mockCache
            .Setup(c => c.Get<List<OrganizationLocationConditionModel>>(It.IsAny<string>()))
            .Returns(new List<OrganizationLocationConditionModel>());

        _service = new LocationMappingService(
            _mockManager.Object,
            _mockQueries.Object,
            _mockConfigQueries.Object,
            _mockCache.Object,
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
    public async Task UpdateLocationMappingAsync_AfterAdd_RunsChildBackfillWithOwnLocationId()
    {
        // Arrange
        var location = NewLocation("hosp-1", name: "Hospital");

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        // Adopt orphans whose PartOfValue points at THIS location's id, under the new mapping id.
        _mockManager.Verify(m => m.SetPartOfIdForChildrenAsync(
            FacilityId, "hosp-1", NewMappingId, It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task UpdateLocationMappingAsync_WhenLocationMatchesOrgCondition_MarksAsOrgLocation()
    {
        // Arrange
        var location = NewLocation("loc-1", name: "University Hospital");
        _mockCache
            .Setup(c => c.Get<List<OrganizationLocationConditionModel>>(It.IsAny<string>()))
            .Returns(new List<OrganizationLocationConditionModel>
            {
                new() { ConditionId = 1, Priority = 1, FhirPath = "Location.name = 'University Hospital'" }
            });

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
        _mockCache
            .Setup(c => c.Get<List<OrganizationLocationConditionModel>>(It.IsAny<string>()))
            .Returns(new List<OrganizationLocationConditionModel>
            {
                new() { ConditionId = 1, Priority = 1, FhirPath = "Location.name = 'University Hospital'" }
            });

        // Act
        await _service.UpdateLocationMappingAsync(FacilityId, location);

        // Assert
        _mockManager.Verify(m => m.CreateAsync(It.Is<CreateOrganizationLocationMappingModel>(c =>
            !c.IsOrgLocation)), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_HonorsConditionPriority_FirstMatchWins()
    {
        // Arrange
        // Higher-priority (1) condition matches; a malformed lower-priority condition must never run.
        var location = NewLocation("loc-1", name: "University Hospital");
        _mockCache
            .Setup(c => c.Get<List<OrganizationLocationConditionModel>>(It.IsAny<string>()))
            .Returns(new List<OrganizationLocationConditionModel>
            {
                new() { ConditionId = 1, Priority = 1, FhirPath = "Location.name = 'University Hospital'" },
                new() { ConditionId = 2, Priority = 2, FhirPath = "this.is.not.valid.fhirpath(" }
            });

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
        _mockCache
            .Setup(c => c.Get<List<OrganizationLocationConditionModel>>(It.IsAny<string>()))
            .Returns((List<OrganizationLocationConditionModel>?)null);

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
        _mockManager.Verify(m => m.SetPartOfIdForChildrenAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
