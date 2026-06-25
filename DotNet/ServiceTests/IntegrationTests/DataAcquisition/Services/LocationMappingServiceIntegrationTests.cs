using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Services;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class LocationMappingServiceIntegrationTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public LocationMappingServiceIntegrationTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// Validates the precondition the service's recovery filter depends on: a duplicate
    /// (FacilityId, LocationId) insert produces a DbUpdateException whose inner SqlException
    /// carries error number 2601/2627. This is the exact shape IsUniqueConstraintViolation keys
    /// on and cannot be constructed in a pure unit test.
    /// </summary>
    [Fact]
    public async Task CreateAsync_DuplicateFacilityAndLocation_ThrowsUniqueConstraintViolation()
    {
        var facilityId = NewId("Fac");
        var locationId = NewId("Loc");

        using (var seedScope = _fixture.ServiceProvider.CreateScope())
        {
            var seedManager = seedScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
            await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = locationId,
                LocationName = "Original",
                IsActive = true,
                IsOrgLocation = false
            });
        }

        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId,
            LocationName = "Duplicate",
            IsActive = true,
            IsOrgLocation = false
        }));

        var sqlException = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains(sqlException.Number, new[] { 2601, 2627 });
    }

    /// <summary>
    /// End-to-end recovery: when the initial existence check misses but a concurrent insert
    /// has already created the row, CreateAsync trips the unique constraint and the service must
    /// recover by re-reading and updating instead of failing. The queries are mocked to reproduce
    /// the race timing (null on the first read, the existing row on the recovery re-read) while the
    /// manager + database are real so the genuine SqlException flows through the filter.
    /// </summary>
    [Fact]
    public async Task UpdateLocationMappingAsync_WhenInsertRacesUniqueConstraint_RecoversByUpdating()
    {
        var facilityId = NewId("Fac");
        var locationId = NewId("Loc");

        // Create a real mapping in the database for the row we are about to insert
        int seededId;
        using (var seedScope = _fixture.ServiceProvider.CreateScope())
        {
            var seedManager = seedScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
            var seeded = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = locationId,
                LocationName = "Original",
                IsActive = true,
                IsOrgLocation = false
            });
            seededId = seeded.LocationMappingId;
        }

        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();

        // Fake two results in sequence to the GetByFacilityIdandLoactionID
        // - first returns null
        // - second returns a record (that was created above)
        var mockQueries = new Mock<IOrganizationLocationMappingQueries>();
        mockQueries
            .SetupSequence(q => q.GetByFacilityIdAndLocationIdAsync(facilityId, locationId))
            .ReturnsAsync((OrganizationLocationMappingModel?)null)              // initial check misses
            .ReturnsAsync(new OrganizationLocationMappingModel                  // recovery re-read finds it
            {
                LocationMappingId = seededId,
                FacilityId = facilityId,
                LocationId = locationId,
                LocationName = "Original",
                IsActive = true,
                IsOrgLocation = false
            });

        var mockConfigQueries = new Mock<IOrganizationLocationConfigurationQueries>();
        mockConfigQueries
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<OrganizationLocationConfigurationModel>());

        // The facility must have at least one active condition, otherwise UpdateLocationMappingAsync
        // short-circuits with NotFoundException before reaching the race-recovery path under test.
        // The FhirPath deliberately doesn't match the bare test Location, so it stays a non-org location.
        var mockCache = new Mock<ICacheService>();
        mockCache
            .Setup(c => c.Get<List<OrganizationLocationConditionModel>>(It.IsAny<string>()))
            .Returns(new List<OrganizationLocationConditionModel>
            {
                new() { FhirPath = "managingOrganization.reference = 'Organization/never'", Priority = 1 }
            });

        // Make the real call, the code checks if the records exists and the mock returns nothing
        // then it does an add which fails because a record does exist.  So it gets the 
        // record and does an update instead.
        var service = new LocationMappingService(
            manager,
            mockQueries.Object,
            mockConfigQueries.Object,
            new Mock<IEncounterMappingQueries>().Object,
            new Mock<IEncounterMappingManager>().Object,
            new Mock<IReferenceResourcesQueries>().Object,
            mockCache.Object,
            new Mock<ILogger<LocationMappingService>>().Object);

        var location = new Location { Id = locationId, Name = "Updated Name" };

        var result = await service.UpdateLocationMappingAsync(facilityId, location);

        Assert.Equal("Updated Name", result.LocationName);

        // Exactly one row remains, carrying the updated name — the duplicate insert did not persist.
        using var verifyScope = _fixture.ServiceProvider.CreateScope();
        var verifyQueries = verifyScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var rows = await verifyQueries.GetByFacilityIdAsync(facilityId);
        Assert.Single(rows);
        Assert.Equal("Updated Name", rows[0].LocationName);
    }
}
