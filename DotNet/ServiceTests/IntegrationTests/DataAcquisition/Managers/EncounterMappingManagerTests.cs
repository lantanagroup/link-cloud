using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class EncounterMappingManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public EncounterMappingManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IEncounterMappingManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        return new EncounterMappingManager(database, dbContext);
    }

    private IEncounterMappingQueries CreateQueries(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new EncounterMappingQueries(database);
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// CreateAsync rejects a facility that isn't configured and a patient that wasn't acquired for it.
    /// Seed the minimal rows those checks read: a FhirQueryConfiguration for the facility and a
    /// DataAcquisitionLog per (facility, patient).
    /// </summary>
    private static async Task SeedFacilityAndPatientsAsync(IServiceScope scope, string facilityId, params string[] patientIds)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        dbContext.Set<FhirQueryConfiguration>().Add(new FhirQueryConfiguration
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            FhirServerBaseUrl = "https://example.org/fhir"
        });

        foreach (var patientId in patientIds)
        {
            dbContext.Set<DataAcquisitionLog>().Add(new DataAcquisitionLog
            {
                FacilityId = facilityId,
                PatientId = patientId
            });
        }

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = NewFacilityId("Facility-ABC");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "Patient-123");

        var locationMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = "Loc-1",
            IsOrgLocation = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var loc2 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = "Loc-2",
            IsOrgLocation = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        var createModel = new CreateEncounterMappingModel
        {
            FacilityId = facilityId,
            PatientId = "Patient-123",
            EncounterId = "Encounter-456",
            MappedToOrg = true,
            OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId, loc2.LocationMappingId }
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal("Patient-123", result.PatientId);
        Assert.Equal("Encounter-456", result.EncounterId);
        Assert.True(result.MappedToOrg);
        Assert.Equal(2, result.EncounterLocations.Count);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = NewFacilityId("Test-Fac");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "Test-Pat");

        var locationMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc10 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId, LocationId = "Loc-10", IsOrgLocation = true, IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var loc20 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId, LocationId = "Loc-20", IsOrgLocation = true, IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var loc30 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId, LocationId = "Loc-30", IsOrgLocation = true, IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = facilityId,
            PatientId = "Test-Pat",
            EncounterId = "Test-Enc",
            MappedToOrg = false
        });

        var updateModel = new UpdateEncounterMappingModel
        {
            MappedToOrg = true,
            OrganizationLocationMappingIds = new List<int> { loc10.LocationMappingId, loc20.LocationMappingId, loc30.LocationMappingId }
        };

        var result = await manager.UpdateByIdAsync(created.EncounterMappingId, updateModel);

        Assert.True(result.MappedToOrg);
        Assert.Equal(3, result.EncounterLocations.Count);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_ReturnsPagedResult()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);
        var fac1 = NewFacilityId("Fac-1");
        var fac2 = NewFacilityId("Fac-2");
        await SeedFacilityAndPatientsAsync(scope, fac1, "Pat-1", "Pat-2");
        await SeedFacilityAndPatientsAsync(scope, fac2, "Pat-3");

        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = fac1, PatientId = "Pat-1", EncounterId = "Enc-1", MappedToOrg = true
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = fac1, PatientId = "Pat-2", EncounterId = "Enc-2", MappedToOrg = false
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = fac2, PatientId = "Pat-3", EncounterId = "Enc-3", MappedToOrg = true
        });

        var search = new EncounterMappingSearchModel { FacilityId = fac1 };
        var result = await queries.SearchAsync(search, 1, 10);

        Assert.Equal(2, result.Metadata.TotalCount);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public async Task SearchAsync_ProjectsLocationIds_CommaJoinableAcrossMultipleLocations()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);
        var facilityId = NewFacilityId("Fac-Loc");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "Pat-1");

        var locMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "Loc-A" });
        var loc2 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "Loc-B" });
        await dbContext.SaveChangesAsync();

        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = facilityId,
            PatientId = "Pat-1",
            EncounterId = "Enc-1",
            MappedToOrg = true,
            OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId, loc2.LocationMappingId }
        });

        var result = await queries.SearchAsync(new EncounterMappingSearchModel { FacilityId = facilityId }, 1, 10);

        var record = Assert.Single(result.Records);
        var locationIds = record.EncounterLocations.Select(l => l.LocationId).ToList();
        Assert.Equal(2, locationIds.Count);
        Assert.Contains("Loc-A", locationIds);
        Assert.Contains("Loc-B", locationIds);
    }

    [Theory]
    [InlineData(SortOrder.Ascending, new[] { "Enc-A", "Enc-B", "Enc-C" })]
    [InlineData(SortOrder.Descending, new[] { "Enc-C", "Enc-B", "Enc-A" })]
    public async Task SearchAsync_SortByEncounterId_OrdersResults(SortOrder sortOrder, string[] expectedOrder)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);
        var facilityId = NewFacilityId("Fac-Sort");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "Pat-1");

        // Insert out of order so the assertion reflects sorting, not insertion order.
        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = facilityId, PatientId = "Pat-1", EncounterId = "Enc-B" });
        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = facilityId, PatientId = "Pat-1", EncounterId = "Enc-A" });
        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = facilityId, PatientId = "Pat-1", EncounterId = "Enc-C" });

        var result = await queries.SearchAsync(
            new EncounterMappingSearchModel { FacilityId = facilityId }, 1, 10, "EncounterId", sortOrder);

        Assert.Equal(expectedOrder, result.Records.Select(r => r.EncounterId).ToArray());
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_DeletesAllForFacility()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);
        var deleteFac = NewFacilityId("Delete-Fac");
        var otherFac = NewFacilityId("Other-Fac");
        await SeedFacilityAndPatientsAsync(scope, deleteFac, "P1", "P2");
        await SeedFacilityAndPatientsAsync(scope, otherFac, "P3");

        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = deleteFac, PatientId = "P1", EncounterId = "E1"
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = deleteFac, PatientId = "P2", EncounterId = "E2"
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = otherFac, PatientId = "P3", EncounterId = "E3"
        });

        await manager.DeleteByFacilityIdAsync(deleteFac);

        var results = await queries.GetByFacilityIdAsync(deleteFac);
        Assert.Empty(results);

        var otherResults = await queries.GetByFacilityIdAsync(otherFac);
        Assert.Single(otherResults);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNaturalKey_ThrowsException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Fac-1");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "Pat-1");
        var model = new CreateEncounterMappingModel
        {
            FacilityId = facilityId, EncounterId = "Enc-1", PatientId = "Pat-1"
        };

        await manager.CreateAsync(model);

        // Second creation with same FacilityId and EncounterId should fail due to unique constraint
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistentId_ThrowsKeyNotFoundException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.UpdateByIdAsync(int.MaxValue, new UpdateEncounterMappingModel()));
    }

    [Fact]
    public async Task DeleteByPatientIdAsync_DeletesMatchingRecords()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);
        var fac1 = NewFacilityId("F1");
        var fac2 = NewFacilityId("F2");
        var targetPatient = $"Target-Pat-{Guid.NewGuid():N}";
        var otherPatient = $"Other-Pat-{Guid.NewGuid():N}";
        await SeedFacilityAndPatientsAsync(scope, fac1, targetPatient, otherPatient);
        await SeedFacilityAndPatientsAsync(scope, fac2, targetPatient);

        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = fac1, EncounterId = "E1", PatientId = targetPatient });
        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = fac2, EncounterId = "E2", PatientId = targetPatient });
        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = fac1, EncounterId = "E3", PatientId = otherPatient });

        await manager.DeleteByPatientIdAsync(targetPatient);

        var targetResults = await queries.GetByPatientIdAsync(targetPatient);
        Assert.Empty(targetResults);

        var otherResults = await queries.GetByPatientIdAsync(otherPatient);
        Assert.Single(otherResults);
    }

    [Fact]
    public async Task CreateAsync_InvalidLocationMappingId_ThrowsBadRequestException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("F1");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "P1");
        var model = new CreateEncounterMappingModel
        {
            FacilityId = facilityId,
            EncounterId = "E1",
            PatientId = "P1",
            OrganizationLocationMappingIds = new List<int> { int.MaxValue } // Non-existent ID
        };

        // The EncounterLocation -> OrganizationLocationMapping FK violation is translated into a
        // BadRequestException that names the invalid id (rather than surfacing the raw DbUpdateException).
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => manager.CreateAsync(model));
        Assert.Contains(int.MaxValue.ToString(), ex.Message);
    }

    [Fact]
    public async Task UpdateByIdAsync_MappedToOrgOnly_DoesNotTouchLocations()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = NewFacilityId("F1");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "P1");

        var locMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "L1" });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = facilityId, EncounterId = "E1", PatientId = "P1", OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId }
        });

        var updateModel = new UpdateEncounterMappingModel { MappedToOrg = false, OrganizationLocationMappingIds = null };
        var updated = await manager.UpdateByIdAsync(created.EncounterMappingId, updateModel);

        Assert.False(updated.MappedToOrg);
        Assert.Single(updated.EncounterLocations);
    }

    [Fact]
    public async Task SearchAsync_PageOutOfBounds_ReturnsEmptyRecordsButCorrectTotalCount()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);
        var facilityId = NewFacilityId("F1");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "P1");

        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = facilityId, EncounterId = "E1", PatientId = "P1" });

        var result = await queries.SearchAsync(new EncounterMappingSearchModel { FacilityId = facilityId }, 2, 10);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.Metadata.TotalCount);
        Assert.Equal(1, result.Metadata.TotalPages);
    }

    [Fact]
    public async Task DeleteByIdAsync_CascadeDeletesLocations()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var facilityId = NewFacilityId("F1");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "P1");

        var locMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "L1" });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = facilityId, EncounterId = "E1", PatientId = "P1", OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId }
        });

        var mappingId = created.EncounterMappingId;
        await manager.DeleteByIdAsync(mappingId);

        var locations = await dbContext.EncounterLocations.Where(l => l.EncounterMappingId == mappingId).ToListAsync();
        Assert.Empty(locations);
    }

    [Fact]
    public async Task UpdateByIdAsync_SyncsLocationsCorrectly()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var facilityId = NewFacilityId("F1");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "P1");

        var locMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "L1" });
        var loc2 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "L2" });
        var loc3 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "L3" });

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = facilityId, EncounterId = "E1", PatientId = "P1", OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId, loc2.LocationMappingId }
        });

        // Update: remove L1, keep L2, add L3
        var updateModel = new UpdateEncounterMappingModel
        {
            OrganizationLocationMappingIds = new List<int> { loc2.LocationMappingId, loc3.LocationMappingId }
        };

        var updated = await manager.UpdateByIdAsync(created.EncounterMappingId, updateModel);

        Assert.Equal(2, updated.EncounterLocations.Count);
        Assert.Contains(updated.EncounterLocations, l => l.OrganizationLocationMappingId == loc2.LocationMappingId);
        Assert.Contains(updated.EncounterLocations, l => l.OrganizationLocationMappingId == loc3.LocationMappingId);
        Assert.DoesNotContain(updated.EncounterLocations, l => l.OrganizationLocationMappingId == loc1.LocationMappingId);
    }

    [Fact]
    public async Task LocationMappingUpdate_CascadesMappedToOrg_UsingAnyOrgSemanticsForMultiLocationEncounter()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var locationMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var encounterMappingManager = CreateManager(scope);

        var facilityId = NewFacilityId("Cascade-OR");
        await SeedFacilityAndPatientsAsync(scope, facilityId, "P1");

        // Location A is an org location; Location B is not.
        var locOrg = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        { FacilityId = facilityId, LocationId = "Loc-Org", IsOrgLocation = true, IsActive = true });
        await dbContext.SaveChangesAsync();
        var locNonOrg = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        { FacilityId = facilityId, LocationId = "Loc-NonOrg", IsOrgLocation = false, IsActive = true });
        await dbContext.SaveChangesAsync();

        // The encounter references BOTH locations and is mapped to org because A is an org location.
        await encounterMappingManager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = facilityId,
            PatientId = "P1",
            EncounterId = "E1",
            MappedToOrg = true,
            OrganizationLocationMappingIds = new List<int> { locOrg.LocationMappingId, locNonOrg.LocationMappingId }
        });

        // Act — touch the NON-org location (it stays non-org), which fires the location->encounter cascade.
        await locationMappingManager.UpdateByIdAsync(locNonOrg.LocationMappingId,
            new UpdateOrganizationLocationMappingModel { IsOrgLocation = false });

        // Assert in a fresh scope — the cascade writes via ExecuteUpdateAsync, which bypasses the
        // change tracker, so a read on the same context would return the stale tracked entity. A new
        // scope reads DB truth (as a later report correlation would).
        using var verifyScope = _fixture.ServiceProvider.CreateScope();
        var verifyQueries = CreateQueries(verifyScope);

        // MappedToOrg must remain TRUE: the encounter still references an org location.
        // The old overwrite-cascade would have cleared it to the non-org location's value.
        var updated = await verifyQueries.GetByFacilityIdAndEncounterIdAsync(facilityId, "E1");
        Assert.NotNull(updated);
        Assert.True(updated!.MappedToOrg);
    }
}
