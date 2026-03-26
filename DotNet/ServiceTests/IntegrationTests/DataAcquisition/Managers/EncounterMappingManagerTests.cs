using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class EncounterMappingManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public EncounterMappingManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IEncounterMappingManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new EncounterMappingManager(database);
    }

    private IEncounterMappingQueries CreateQueries(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new EncounterMappingQueries(database);
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var locationMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Facility-ABC",
            LocationId = "Loc-1",
            IsOrgLocation = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var loc2 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Facility-ABC",
            LocationId = "Loc-2",
            IsOrgLocation = true,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        var createModel = new CreateEncounterMappingModel
        {
            FacilityId = "Facility-ABC",
            PatientId = "Patient-123",
            EncounterId = "Encounter-456",
            MappedToOrg = true,
            OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId, loc2.LocationMappingId }
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal("Facility-ABC", result.FacilityId);
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
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var locationMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc10 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Test-Fac", LocationId = "Loc-10", IsOrgLocation = true, IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var loc20 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Test-Fac", LocationId = "Loc-20", IsOrgLocation = true, IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var loc30 = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Test-Fac", LocationId = "Loc-30", IsOrgLocation = true, IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "Test-Fac",
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);

        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "Fac-1", PatientId = "Pat-1", EncounterId = "Enc-1", MappedToOrg = true
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "Fac-1", PatientId = "Pat-2", EncounterId = "Enc-2", MappedToOrg = false
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "Fac-2", PatientId = "Pat-3", EncounterId = "Enc-3", MappedToOrg = true
        });

        var search = new EncounterMappingSearchModel { FacilityId = "Fac-1" };
        var result = await queries.SearchAsync(search, 1, 10);

        Assert.Equal(2, result.Metadata.TotalCount);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_DeletesAllForFacility()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);

        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "Delete-Fac", PatientId = "P1", EncounterId = "E1"
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "Delete-Fac", PatientId = "P2", EncounterId = "E2"
        });
        await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "Other-Fac", PatientId = "P3", EncounterId = "E3"
        });

        await manager.DeleteByFacilityIdAsync("Delete-Fac");

        var results = await queries.GetByFacilityIdAsync("Delete-Fac");
        Assert.Empty(results);

        var otherResults = await queries.GetByFacilityIdAsync("Other-Fac");
        Assert.Single(otherResults);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNaturalKey_ThrowsException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var model = new CreateEncounterMappingModel
        {
            FacilityId = "Fac-1", EncounterId = "Enc-1", PatientId = "Pat-1"
        };

        await manager.CreateAsync(model);

        // Second creation with same FacilityId and EncounterId should fail due to unique constraint
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistentId_ThrowsKeyNotFoundException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.UpdateByIdAsync(9999, new UpdateEncounterMappingModel()));
    }

    [Fact]
    public async Task DeleteByPatientIdAsync_DeletesMatchingRecords()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);

        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = "F1", EncounterId = "E1", PatientId = "Target-Pat" });
        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = "F2", EncounterId = "E2", PatientId = "Target-Pat" });
        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = "F1", EncounterId = "E3", PatientId = "Other-Pat" });

        await manager.DeleteByPatientIdAsync("Target-Pat");

        var targetResults = await queries.GetByPatientIdAsync("Target-Pat");
        Assert.Empty(targetResults);

        var otherResults = await queries.GetByPatientIdAsync("Other-Pat");
        Assert.Single(otherResults);
    }

    [Fact]
    public async Task CreateAsync_InvalidLocationMappingId_ThrowsException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var model = new CreateEncounterMappingModel
        {
            FacilityId = "F1",
            EncounterId = "E1",
            PatientId = "P1",
            OrganizationLocationMappingIds = new List<int> { 9999 } // Non-existent ID
        };

        // SQLite should throw a FK constraint violation
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task UpdateByIdAsync_MappedToOrgOnly_DoesNotTouchLocations()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var locMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "F1", LocationId = "L1" });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "F1", EncounterId = "E1", PatientId = "P1", OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId }
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var queries = CreateQueries(scope);

        await manager.CreateAsync(new CreateEncounterMappingModel { FacilityId = "F1", EncounterId = "E1", PatientId = "P1" });

        var result = await queries.SearchAsync(new EncounterMappingSearchModel(), 2, 10);

        Assert.Empty(result.Records);
        Assert.Equal(1, result.Metadata.TotalCount);
        Assert.Equal(1, result.Metadata.TotalPages);
    }

    [Fact]
    public async Task DeleteByIdAsync_CascadeDeletesLocations()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var locMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "F1", LocationId = "L1" });
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "F1", EncounterId = "E1", PatientId = "P1", OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId }
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var locMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var loc1 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "F1", LocationId = "L1" });
        var loc2 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "F1", LocationId = "L2" });
        var loc3 = await locMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "F1", LocationId = "L3" });

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = "F1", EncounterId = "E1", PatientId = "P1", OrganizationLocationMappingIds = new List<int> { loc1.LocationMappingId, loc2.LocationMappingId }
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
}
