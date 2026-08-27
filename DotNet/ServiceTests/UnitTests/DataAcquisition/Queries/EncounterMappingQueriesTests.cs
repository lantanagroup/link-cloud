using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Moq;
using System.Linq.Expressions;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Queries;

[Trait("Category", "UnitTests")]
public class EncounterMappingQueriesTests
{
    private const string FacilityId = "facility-1";
    private const string PatientId = "patient-1";

    private readonly Mock<IDatabase> _mockDatabase = new();
    private readonly Mock<IEntityRepository<EncounterMapping>> _mockMappingRepo = new();
    private readonly Mock<IEntityRepository<EncounterLocation>> _mockLocationRepo = new();
    private readonly Mock<IEntityRepository<OrganizationLocationMapping>> _mockOrgLocationRepo = new();
    private readonly EncounterMappingQueries _queries;

    public EncounterMappingQueriesTests()
    {
        _mockDatabase.Setup(d => d.EncounterMappingRepository).Returns(_mockMappingRepo.Object);
        _mockDatabase.Setup(d => d.EncounterLocationRepository).Returns(_mockLocationRepo.Object);
        _mockDatabase.Setup(d => d.LocationMappingRepository).Returns(_mockOrgLocationRepo.Object);

        _queries = new EncounterMappingQueries(_mockDatabase.Object);
    }

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_ProjectsFullOrganizationLocationDetail()
    {
        // Arrange — one encounter referencing one location that resolves to the organization.
        SetupMappings(new EncounterMapping { EncounterMappingId = 1, FacilityId = FacilityId, PatientId = PatientId, EncounterId = "enc-1", MappedToOrg = true });

        SetupEncounterLocations(new EncounterLocation
        {
            EncounterLocationId = 10,
            EncounterMappingId = 1,
            OrganizationLocationMappingId = 100
        });

        SetupOrganizationLocations(new OrganizationLocationMapping
        {
            LocationMappingId = 100,
            FacilityId = FacilityId,
            LocationId = "loc-org",
            LocationName = "5 West Medical ICU",
            LocationAlias = "1027-4",
            PartOfValue = "loc-root",
            IsOrgLocation = true
        });

        // Act
        var result = await _queries.GetByFacilityIdAndPatientIdAsync(FacilityId, PatientId, CancellationToken.None);

        // Assert — every field the org-location outcome reports survives the projection. Before these were
        // carried, the query materialized the same entities and kept only LocationId.
        var location = Assert.Single(Assert.Single(result).EncounterLocations);
        Assert.Equal("loc-org", location.LocationId);
        Assert.Equal("5 West Medical ICU", location.LocationName);
        Assert.Equal("1027-4", location.LocationAlias);
        Assert.Equal("loc-root", location.PartOfValue);
        Assert.True(location.IsOrgLocation);
    }

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_UnresolvedOrganizationLocation_LeavesDetailNull()
    {
        // Arrange — an EncounterLocation whose OrganizationLocationMapping cannot be resolved. The foreign
        // key makes this unreachable in practice; the projection still must not invent values.
        SetupMappings(new EncounterMapping { EncounterMappingId = 1, FacilityId = FacilityId, PatientId = PatientId, EncounterId = "enc-1" });
        SetupEncounterLocations(new EncounterLocation { EncounterLocationId = 10, EncounterMappingId = 1, OrganizationLocationMappingId = 999 });
        SetupOrganizationLocations();

        // Act
        var result = await _queries.GetByFacilityIdAndPatientIdAsync(FacilityId, PatientId, CancellationToken.None);

        // Assert
        var location = Assert.Single(Assert.Single(result).EncounterLocations);
        Assert.Null(location.LocationId);
        Assert.Null(location.LocationName);
        Assert.Null(location.LocationAlias);
        Assert.Null(location.PartOfValue);
        Assert.False(location.IsOrgLocation);
    }

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_ManyEncountersAndLocations_UsesTwoBatchedQueries()
    {
        // Arrange — three encounters across three distinct locations. Enough that a per-row lookup would
        // show up as repeated repository calls.
        SetupMappings(
            new EncounterMapping { EncounterMappingId = 1, FacilityId = FacilityId, PatientId = PatientId, EncounterId = "enc-1", MappedToOrg = true },
            new EncounterMapping { EncounterMappingId = 2, FacilityId = FacilityId, PatientId = PatientId, EncounterId = "enc-2", MappedToOrg = true },
            new EncounterMapping { EncounterMappingId = 3, FacilityId = FacilityId, PatientId = PatientId, EncounterId = "enc-3", MappedToOrg = false });

        SetupEncounterLocations(
            new EncounterLocation { EncounterLocationId = 10, EncounterMappingId = 1, OrganizationLocationMappingId = 100 },
            new EncounterLocation { EncounterLocationId = 11, EncounterMappingId = 2, OrganizationLocationMappingId = 101 },
            new EncounterLocation { EncounterLocationId = 12, EncounterMappingId = 3, OrganizationLocationMappingId = 102 });

        SetupOrganizationLocations(
            new OrganizationLocationMapping { LocationMappingId = 100, LocationId = "loc-a", IsOrgLocation = true },
            new OrganizationLocationMapping { LocationMappingId = 101, LocationId = "loc-b", IsOrgLocation = true },
            new OrganizationLocationMapping { LocationMappingId = 102, LocationId = "loc-c", IsOrgLocation = false });

        // Act
        var result = await _queries.GetByFacilityIdAndPatientIdAsync(FacilityId, PatientId, CancellationToken.None);

        // Assert — widening the projection must not have turned the batched load into an N+1. The whole
        // reason the detail is free is that these two queries already materialized the entities.
        _mockLocationRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<EncounterLocation, bool>>>()), Times.Once);
        _mockOrgLocationRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationLocationMapping, bool>>>()), Times.Once);

        Assert.Equal(3, result.Count);
        Assert.All(result, mapping => Assert.Single(mapping.EncounterLocations));
    }

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_NoMappings_SkipsLocationQueriesEntirely()
    {
        // Arrange
        SetupMappings();

        // Act
        var result = await _queries.GetByFacilityIdAndPatientIdAsync(FacilityId, PatientId, CancellationToken.None);

        // Assert — nothing to join against, so neither location query runs.
        Assert.Empty(result);
        _mockLocationRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<EncounterLocation, bool>>>()), Times.Never);
        _mockOrgLocationRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationLocationMapping, bool>>>()), Times.Never);
    }

    private void SetupMappings(params EncounterMapping[] mappings) =>
        _mockMappingRepo
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<EncounterMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings.ToList());

    private void SetupEncounterLocations(params EncounterLocation[] locations) =>
        _mockLocationRepo
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<EncounterLocation, bool>>>()))
            .ReturnsAsync(locations.ToList());

    private void SetupOrganizationLocations(params OrganizationLocationMapping[] organizationLocations) =>
        _mockOrgLocationRepo
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationLocationMapping, bool>>>()))
            .ReturnsAsync(organizationLocations.ToList());
}
