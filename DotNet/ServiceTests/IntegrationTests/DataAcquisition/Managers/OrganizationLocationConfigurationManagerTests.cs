using System.Text.Json;
using LantanaGroup.Link.DataAcquisition.Domain.Application;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationConfigurationManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationConfigurationManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrganizationLocationConfigurationManager CreateManager(IServiceScope scope)
       {
           var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
           var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
           var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
           var locationMappingService = scope.ServiceProvider.GetRequiredService<ILocationMappingService>();
           var locationResolutionValidator = scope.ServiceProvider.GetRequiredService<ILocationResolutionValidator>();
           return new OrganizationLocationConfigurationManager(database, queries, locationResolutionValidator, cacheService, locationMappingService);
       }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_ValidModelWithConditions_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Nebraska");

        var createModel = new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = facilityId,
            Description = "Nebraska Epic Config",
            IsActive = true,
            Conditions = new List<CreateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", Priority = 1 }
            }
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal("Nebraska Epic Config", result.Description);
        Assert.True(result.IsActive);
        Assert.Single(result.Conditions);
        Assert.Equal("identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", result.Conditions[0].FhirPath);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = NewFacilityId("Test-Update-Id"),
            Description = "Old Description"
        });

        var updateModel = new UpdateOrganizationLocationConfigurationModel
        {
            Description = "New Description",
            IsActive = false,
            Conditions = new List<UpdateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "managingOrganization.reference = 'Organization/123'", Priority = 1 }
            }
        };

        var result = await manager.UpdateByIdAsync(created.ConfigId, updateModel);

        Assert.Equal("New Description", result.Description);
        Assert.False(result.IsActive);
        Assert.Single(result.Conditions);
    }

    [Fact]
    public async Task UpdateByIdAsync_InvalidatesConditionsCache()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var manager = CreateManager(scope);

        var facilityId = NewFacilityId("Cache-Invalidate");
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = facilityId,
            Description = "Initial",
            Conditions = new List<CreateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "Location.name = 'A'", Priority = 1 }
            }
        });

        // Simulate the read side (LocationMappingService) having populated the conditions cache.
        var cacheKey = OrgLocationCacheKeys.Conditions(facilityId);
        cacheService.Set(cacheKey,
            new List<OrganizationLocationConditionModel> { new() { ConditionId = 1, FhirPath = "Location.name = 'A'", Priority = 1 } },
            TimeSpan.FromHours(1), ExpirationType.Absolute);
        Assert.NotNull(cacheService.Get<List<OrganizationLocationConditionModel>?>(cacheKey));

        // Act — editing the configuration must evict the cached conditions immediately.
        await manager.UpdateByIdAsync(created.ConfigId, new UpdateOrganizationLocationConfigurationModel
        {
            Conditions = new List<UpdateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "Location.name = 'B'", Priority = 1 }
            }
        });

        // Assert — stale conditions are gone, so the next read repopulates from the database.
        Assert.Null(cacheService.Get<List<OrganizationLocationConditionModel>?>(cacheKey));
    }

    [Fact]
    public async Task UpdateByFacilityIdAsync_MultipleConfigs_UpdatesAll()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Multi-Facility");
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId, Description = "Config A" });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId, Description = "Config B" });

        var updateModel = new UpdateOrganizationLocationConfigurationModel { Description = "All Updated" };

        var result = await manager.UpdateByFacilityIdAsync(facilityId, updateModel);

        Assert.All(result, x => x.Description.Equals("All Updated"));
    }

    [Fact]
    public async Task DeleteByIdAsync_Existing_DeletesSuccessfully()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = NewFacilityId("Delete-By-Id") });

        await manager.DeleteByIdAsync(created.ConfigId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetByIdAsync(created.ConfigId));
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_Multiple_DeletesAll()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Delete-All");
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId });

        await manager.DeleteByFacilityIdAsync(facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var search = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel { FacilityId = facilityId });
        Assert.Empty(search.Records);
    }

    [Fact]
    public async Task UpdateByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            manager.UpdateByIdAsync(99999, new UpdateOrganizationLocationConfigurationModel()));
    }

    [Fact]
    public async Task CreateAsync_ReevaluatesCachedLocations_FlippingIsOrgLocationAndMappedToOrg()
    {
        var facilityId = NewFacilityId("Cfg-Reeval");
        var orgReference = "Organization/org-1";
        var locationId = $"Loc_{Guid.NewGuid():N}";

        var location = new Location
        {
            Id = locationId,
            Name = "ICU",
            ManagingOrganization = new ResourceReference(orgReference)
        };

        // Arrange: a cached Location body, a mapping currently flagged NON-org, and an encounter
        // mapping linked to it that is therefore NOT mapped to org.
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            var locationMappingManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
            var encounterMappingManager = scope.ServiceProvider.GetRequiredService<IEncounterMappingManager>();

            // EncounterMappingManager.CreateAsync validates the facility is configured and the patient
            // was acquired for it, so seed the rows those checks read.
            dbContext.Set<FhirQueryConfiguration>().Add(new FhirQueryConfiguration
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                FhirServerBaseUrl = "https://example.org/fhir"
            });
            dbContext.Set<DataAcquisitionLog>().Add(new DataAcquisitionLog { FacilityId = facilityId, PatientId = "P1" });

            dbContext.ReferenceResources.Add(new ReferenceResources
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                ResourceId = locationId,
                ResourceType = ResourceType.Location.ToString(),
                QueryPhase = QueryPhase.Initial,
                ReferenceResource = JsonSerializer.Serialize<Resource>(location, LinkFhirSerializerOptions.ForFhirLenientSerialization)
            });
            await dbContext.SaveChangesAsync();

            var mapping = await locationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = locationId,
                LocationName = "ICU",
                IsOrgLocation = false,
                IsActive = true
            });
            await dbContext.SaveChangesAsync();

            await encounterMappingManager.CreateAsync(new CreateEncounterMappingModel
            {
                FacilityId = facilityId,
                PatientId = "P1",
                EncounterId = "E1",
                MappedToOrg = false,
                OrganizationLocationMappingIds = new List<int> { mapping.LocationMappingId }
            });
        }

        // Act: create a configuration whose condition matches the cached Location. This must trigger
        // re-evaluation of the facility's already-cached Locations.
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var manager = CreateManager(scope);
            await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel
            {
                FacilityId = facilityId,
                Description = "Now matches the cached Location",
                IsActive = true,
                Conditions = new List<CreateOrganizationLocationConditionModel>
                {
                    new() { FhirPath = $"managingOrganization.reference = '{orgReference}'", Priority = 1 }
                }
            });
        }

        // Assert in a fresh scope.
        using (var verify = _fixture.ServiceProvider.CreateScope())
        {
            var locationQueries = verify.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
            var encounterQueries = verify.ServiceProvider.GetRequiredService<IEncounterMappingQueries>();

            var updatedMapping = await locationQueries.GetByFacilityIdAndLocationIdAsync(facilityId, locationId);
            Assert.NotNull(updatedMapping);
            Assert.True(updatedMapping!.IsOrgLocation);

            var updatedEncounter = await encounterQueries.GetByFacilityIdAndEncounterIdAsync(facilityId, "E1");
            Assert.NotNull(updatedEncounter);
            Assert.True(updatedEncounter!.MappedToOrg);
        }
    }
}
