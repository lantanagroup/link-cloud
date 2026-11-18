using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationMappingManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationMappingManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrganizationLocationMappingManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var encounterMappingManager = scope.ServiceProvider.GetRequiredService<IEncounterMappingManager>();
        return new OrganizationLocationMappingManager(database, dbContext, encounterMappingManager);
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
    private static string NewLocationId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Facility-ABC");
        var locationId = NewLocationId("Location-123");

        var createModel = new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId,
            LocationName = "Main Hospital Bed 101",
            LocationAlias = "ICU-Bed-101",
            PartOfValue = "Location-Parent-001",
            PartOfId = null,
            IsOrgLocation = true,
            IsActive = true
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal(locationId, result.LocationId);
        Assert.Equal("Main Hospital Bed 101", result.LocationName);
        Assert.True(result.IsOrgLocation);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = NewFacilityId("Test-Fac"),
            LocationId = NewLocationId("Loc-001"),
            IsOrgLocation = false
        });

        var updateModel = new UpdateOrganizationLocationMappingModel
        {
            LocationName = "Updated Name",
            IsOrgLocation = true,
            IsActive = false
        };

        var result = await manager.UpdateByIdAsync(created.LocationMappingId, updateModel);

        Assert.Equal("Updated Name", result.LocationName);
        Assert.True(result.IsOrgLocation);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task UpdateByFacilityIdAndLocationIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Fac-XYZ");
        var locationId = NewLocationId("Loc-555");
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId,
            LocationName = "Old Name"
        });

        var updateModel = new UpdateOrganizationLocationMappingModel { LocationName = "New Name" };

        var result = await manager.UpdateByFacilityIdAndLocationIdAsync(facilityId, locationId, updateModel);

        Assert.Equal("New Name", result.LocationName);
    }

    [Fact]
    public async Task SetParentForChildrenAsync_WhenChildAlreadyLinked_AppliesOrgParentValue()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var facilityId = NewFacilityId("Linked-Child");
        var parentLocationId = NewLocationId("Parent");
        var childLocationId = NewLocationId("Child");

        var parent = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = parentLocationId,
            LocationName = "Hospital",
            IsOrgLocation = true,
            IsActive = true
        });

        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = childLocationId,
            LocationName = "ED",
            PartOfValue = parentLocationId,
            PartOfId = parent.LocationMappingId,
            IsOrgLocation = false,
            IsActive = true
        });

        var updatedCount = await manager.SetParentForChildrenAsync(
            facilityId,
            parentLocationId,
            parent.LocationMappingId,
            isOrgLocation: true,
            CancellationToken.None);

        var child = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, childLocationId);

        Assert.Equal(1, updatedCount);
        Assert.NotNull(child);
        Assert.Equal(parent.LocationMappingId, child!.PartOfId);
        Assert.True(child.IsOrgLocation);
    }

    [Fact]
    public async Task SetParentForChildrenAsync_WhenOrgParentArrivesLate_AppliesOrgLocationToAllDescendants()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var facilityId = NewFacilityId("Late-Parent");
        var parentLocationId = NewLocationId("Parent");
        var childLocationId = NewLocationId("Child");
        var grandchildLocationId = NewLocationId("Grandchild");
        var greatGrandchildLocationId = NewLocationId("GreatGrandchild");

        var child = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = childLocationId,
            LocationName = "ED",
            PartOfValue = parentLocationId,
            IsOrgLocation = false,
            IsActive = true
        });

        var grandchild = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = grandchildLocationId,
            LocationName = "ED Bed 1",
            PartOfValue = childLocationId,
            PartOfId = child.LocationMappingId,
            IsOrgLocation = false,
            IsActive = true
        });

        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = greatGrandchildLocationId,
            LocationName = "ED Bed 1 Slot",
            PartOfValue = grandchildLocationId,
            PartOfId = grandchild.LocationMappingId,
            IsOrgLocation = false,
            IsActive = true
        });

        var parent = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = parentLocationId,
            LocationName = "Hospital",
            IsOrgLocation = true,
            IsActive = true
        });

        var updatedCount = await manager.SetParentForChildrenAsync(
            facilityId,
            parentLocationId,
            parent.LocationMappingId,
            isOrgLocation: true,
            CancellationToken.None);

        var updatedChild = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, childLocationId);
        var updatedGrandchild = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, grandchildLocationId);
        var updatedGreatGrandchild = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, greatGrandchildLocationId);

        Assert.Equal(3, updatedCount);
        Assert.NotNull(updatedChild);
        Assert.NotNull(updatedGrandchild);
        Assert.NotNull(updatedGreatGrandchild);
        Assert.Equal(parent.LocationMappingId, updatedChild!.PartOfId);
        Assert.Equal(child.LocationMappingId, updatedGrandchild!.PartOfId);
        Assert.Equal(grandchild.LocationMappingId, updatedGreatGrandchild!.PartOfId);
        Assert.True(updatedChild.IsOrgLocation);
        Assert.True(updatedGrandchild.IsOrgLocation);
        Assert.True(updatedGreatGrandchild.IsOrgLocation);
    }

    [Fact]
    public async Task SetParentForChildrenAsync_WhenChildrenAlreadyCurrent_WritesNothing()
    {
        var facilityId = NewFacilityId("Current");
        var parentLocationId = NewLocationId("Parent");
        int parentMappingId;
        int childMappingId;

        using (var seedScope = _fixture.ServiceProvider.CreateScope())
        {
            var seedManager = CreateManager(seedScope);
            var parent = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = parentLocationId,
                IsOrgLocation = true,
                IsActive = true
            });
            var child = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = NewLocationId("Child"),
                PartOfValue = parentLocationId,
                PartOfId = parent.LocationMappingId,
                IsOrgLocation = true,
                IsActive = true
            });
            parentMappingId = parent.LocationMappingId;
            childMappingId = child.LocationMappingId;

            await CreateEncounterMappingAsync(seedScope, facilityId, "Enc-1", mappedToOrg: true, childMappingId);
        }

        var childBefore = await ReadLocationMappingAsync(childMappingId);
        var encounterBefore = await ReadEncounterMappingAsync(facilityId, "Enc-1");

        using var scope = _fixture.ServiceProvider.CreateScope();
        var updatedCount = await CreateManager(scope).SetParentForChildrenAsync(
            facilityId,
            parentLocationId,
            parentMappingId,
            isOrgLocation: true,
            CancellationToken.None);

        var childAfter = await ReadLocationMappingAsync(childMappingId);
        var encounterAfter = await ReadEncounterMappingAsync(facilityId, "Enc-1");

        Assert.Equal(0, updatedCount);
        Assert.Equal(childBefore.ModifiedDate, childAfter.ModifiedDate);
        Assert.Equal(encounterBefore.ModifiedDate, encounterAfter.ModifiedDate);
    }

    [Fact]
    public async Task SetParentForChildrenAsync_WhenChildPromoted_RecomputesItsEncounters()
    {
        var facilityId = NewFacilityId("Promote");
        var parentLocationId = NewLocationId("Parent");
        int parentMappingId;

        using (var seedScope = _fixture.ServiceProvider.CreateScope())
        {
            var seedManager = CreateManager(seedScope);
            var parent = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = parentLocationId,
                IsOrgLocation = true,
                IsActive = true
            });
            var child = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = NewLocationId("Child"),
                PartOfValue = parentLocationId,
                PartOfId = parent.LocationMappingId,
                IsOrgLocation = false,
                IsActive = true
            });
            parentMappingId = parent.LocationMappingId;

            await CreateEncounterMappingAsync(seedScope, facilityId, "Enc-1", mappedToOrg: false, child.LocationMappingId);
        }

        using var scope = _fixture.ServiceProvider.CreateScope();
        await CreateManager(scope).SetParentForChildrenAsync(
            facilityId,
            parentLocationId,
            parentMappingId,
            isOrgLocation: true,
            CancellationToken.None);

        var encounter = await ReadEncounterMappingAsync(facilityId, "Enc-1");

        Assert.True(encounter.MappedToOrg);
    }

    [Fact]
    public async Task SetParentForChildrenAsync_WhenParentNotOrg_LinksChildWithoutTouchingDescendants()
    {
        var facilityId = NewFacilityId("NonOrg");
        var parentLocationId = NewLocationId("Parent");
        var childLocationId = NewLocationId("Child");
        int parentMappingId;
        int childMappingId;
        int grandchildMappingId;

        using (var seedScope = _fixture.ServiceProvider.CreateScope())
        {
            var seedManager = CreateManager(seedScope);
            var child = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = childLocationId,
                PartOfValue = parentLocationId,
                IsOrgLocation = false,
                IsActive = true
            });
            var grandchild = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = NewLocationId("Grandchild"),
                PartOfValue = childLocationId,
                PartOfId = child.LocationMappingId,
                IsOrgLocation = false,
                IsActive = true
            });
            var parent = await seedManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = parentLocationId,
                IsOrgLocation = false,
                IsActive = true
            });
            parentMappingId = parent.LocationMappingId;
            childMappingId = child.LocationMappingId;
            grandchildMappingId = grandchild.LocationMappingId;

            await CreateEncounterMappingAsync(seedScope, facilityId, "Enc-1", mappedToOrg: false, childMappingId);
        }

        var grandchildBefore = await ReadLocationMappingAsync(grandchildMappingId);
        var encounterBefore = await ReadEncounterMappingAsync(facilityId, "Enc-1");

        using var scope = _fixture.ServiceProvider.CreateScope();
        var updatedCount = await CreateManager(scope).SetParentForChildrenAsync(
            facilityId,
            parentLocationId,
            parentMappingId,
            isOrgLocation: false,
            CancellationToken.None);

        var childAfter = await ReadLocationMappingAsync(childMappingId);
        var grandchildAfter = await ReadLocationMappingAsync(grandchildMappingId);
        var encounterAfter = await ReadEncounterMappingAsync(facilityId, "Enc-1");

        Assert.Equal(1, updatedCount);
        Assert.Equal(parentMappingId, childAfter.PartOfId);
        Assert.False(childAfter.IsOrgLocation);
        Assert.Equal(grandchildBefore.ModifiedDate, grandchildAfter.ModifiedDate);
        Assert.Equal(encounterBefore.ModifiedDate, encounterAfter.ModifiedDate);
    }

    [Fact]
    public async Task DeleteByIdAsync_Existing_DeletesRecord()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = NewFacilityId("Delete-Test"),
            LocationId = NewLocationId("Loc-999")
        });

        await manager.DeleteByIdAsync(created.LocationMappingId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetByIdAsync(created.LocationMappingId));
    }

    [Fact]
    public async Task DeleteByFacilityIdAndLocationIdAsync_Existing_DeletesRecord()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Fac-Delete");
        var locationId = NewLocationId("Loc-Delete");
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId
        });

        await manager.DeleteByFacilityIdAndLocationIdAsync(facilityId, locationId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        Assert.Null(await queries.GetByFacilityIdAndLocationIdAsync(facilityId, locationId));
    }

    [Fact]
    public async Task UpdateByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            manager.UpdateByIdAsync(99999, new UpdateOrganizationLocationMappingModel()));
    }

    private static async Task CreateEncounterMappingAsync(IServiceScope scope,
                                                          string facilityId,
                                                          string encounterId,
                                                          bool mappedToOrg,
                                                          int locationMappingId)
    {
        var encounterMappingManager = scope.ServiceProvider.GetRequiredService<IEncounterMappingManager>();
        await encounterMappingManager.CreateFromAcquisitionAsync(new CreateEncounterMappingModel
        {
            FacilityId = facilityId,
            PatientId = "Patient-1",
            EncounterId = encounterId,
            MappedToOrg = mappedToOrg,
            OrganizationLocationMappingIds = [locationMappingId]
        });
    }

    // The manager writes through ExecuteUpdateAsync, which bypasses the change tracker, so reads use a
    // fresh scope to see what the database holds.
    private async Task<OrganizationLocationMapping> ReadLocationMappingAsync(int locationMappingId)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        return await dbContext.OrganizationLocationMappings
            .AsNoTracking()
            .SingleAsync(m => m.LocationMappingId == locationMappingId);
    }

    private async Task<EncounterMapping> ReadEncounterMappingAsync(string facilityId, string encounterId)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        return await dbContext.EncounterMappings
            .AsNoTracking()
            .SingleAsync(m => m.FacilityId == facilityId && m.EncounterId == encounterId);
    }
}
