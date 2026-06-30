using System.Text.Json;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Services;

/// <summary>
/// LEGLINK-139: "Add to mapping table when Location resources are queried."
///
/// When a facility is configured for Encounter Location resolution, Data Acquisition must
/// store each queried Location into the OrganizationLocationMapping table. For each
/// Location.Id, the mapping is inserted only when it does not already exist, and there must
/// never be more than one entry per (FacilityId, Location.Id).
///
/// These tests drive the production <see cref="LocationMappingService"/> against the real
/// configuration and mapping tables (via the fixture's SQL Server) and a real
/// <see cref="InMemoryCacheService"/>. Only the FHIR API / Kafka edges are absent — the
/// insert-or-skip decision, the org-location classification, and the database write are all
/// exercised end-to-end, which is what the existing unit tests cannot cover.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class LocationMappingAcquisitionTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public LocationMappingAcquisitionTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
    private static string NewLocationId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// Builds the real service from scope-resolved dependencies, mirroring how DataAcquisition
    /// wires it in production. Each call uses a fresh <see cref="InMemoryCacheService"/> so cached
    /// conditions never leak across assertions within a test.
    /// </summary>
    private static LocationMappingService CreateService(IServiceScope scope)
    {
        return new LocationMappingService(
            scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>(),
            scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>(),
            scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>(),
            scope.ServiceProvider.GetRequiredService<IEncounterMappingQueries>(),
            scope.ServiceProvider.GetRequiredService<IEncounterMappingManager>(),
            scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>(),
            new InMemoryCacheService(new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 })),
            new Mock<IResourceCache>().Object,
            NullLogger<LocationMappingService>.Instance);
    }

    /// <summary>
    /// Seeds an active Location configuration whose single condition flags a Location as an
    /// org-location when its managingOrganization points at <paramref name="orgReference"/>.
    /// This is the configuration the requirement gates on ("configured to perform Encounter
    /// Location resolutions").
    /// </summary>
    private async Task SeedActiveConfigAsync(string facilityId, string orgReference)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var configManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        await configManager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = facilityId,
            Description = "Integration test config",
            IsActive = true,
            Conditions = new List<CreateOrganizationLocationConditionModel>
            {
                new() { FhirPath = $"managingOrganization.reference = '{orgReference}'", Priority = 1 }
            }
        });
    }

    [Fact]
    public async Task IsConfigured_NoActiveConfiguration_ReturnsFalse()
    {
        var facilityId = NewFacilityId("Unconfigured");

        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = CreateService(scope);

        Assert.False(await service.IsConfigured(facilityId, CancellationToken.None));
    }

    [Fact]
    public async Task IsConfigured_WithActiveConfiguration_ReturnsTrue()
    {
        var facilityId = NewFacilityId("Configured");
        await SeedActiveConfigAsync(facilityId, "Organization/org-1");

        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = CreateService(scope);

        Assert.True(await service.IsConfigured(facilityId, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_ConfiguredFacilityNewLocation_InsertsMapping()
    {
        var facilityId = NewFacilityId("Insert");
        var orgReference = "Organization/org-1";
        await SeedActiveConfigAsync(facilityId, orgReference);

        var locationId = NewLocationId("Loc");
        var location = new Location
        {
            Id = locationId,
            Name = "Med-Surg Unit 3",
            ManagingOrganization = new ResourceReference(orgReference)
        };

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope);
            var result = await service.UpdateLocationMappingAsync(facilityId, location, cancellationToken: CancellationToken.None);

            Assert.Equal(facilityId, result.FacilityId);
            Assert.Equal(locationId, result.LocationId);
            Assert.Equal("Med-Surg Unit 3", result.LocationName);
            Assert.True(result.IsActive);
            // managingOrganization matched the active condition, so it is flagged as an org-location.
            Assert.True(result.IsOrgLocation);
        }

        // The row is durable: a fresh scope re-reads exactly one persisted mapping.
        using var verifyScope = _fixture.ServiceProvider.CreateScope();
        var queries = verifyScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var rows = await queries.GetByFacilityIdAsync(facilityId);
        var row = Assert.Single(rows);
        Assert.Equal(locationId, row.LocationId);
        Assert.Equal("Med-Surg Unit 3", row.LocationName);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_SameLocationQueriedTwice_DoesNotDuplicate()
    {
        var facilityId = NewFacilityId("NoDup");
        var orgReference = "Organization/org-1";
        await SeedActiveConfigAsync(facilityId, orgReference);

        var locationId = NewLocationId("Loc");

        // Two separate acquisitions of the same Location.Id (e.g. it appears across patients/runs).
        // The second query carries an updated name to prove the existing row is updated in place,
        // not inserted again.
        using (var scope1 = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope1);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location { Id = locationId, Name = "Original Name", ManagingOrganization = new ResourceReference(orgReference) },
                cancellationToken: CancellationToken.None);
        }

        using (var scope2 = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope2);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location { Id = locationId, Name = "Renamed", ManagingOrganization = new ResourceReference(orgReference) },
                cancellationToken: CancellationToken.None);
        }

        // Requirement: at most one entry per (FacilityId, Location.Id).
        using var verifyScope = _fixture.ServiceProvider.CreateScope();
        var queries = verifyScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var rows = await queries.GetByFacilityIdAsync(facilityId);
        var row = Assert.Single(rows);
        Assert.Equal(locationId, row.LocationId);
        Assert.Equal("Renamed", row.LocationName);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_ParentQueriedAfterChild_BackfillsChildPartOfId()
    {
        var facilityId = NewFacilityId("Backfill");
        var orgReference = "Organization/org-1";
        await SeedActiveConfigAsync(facilityId, orgReference);

        var parentLocationId = NewLocationId("Parent");
        var childLocationId = NewLocationId("Child");

        // The child arrives first, referencing a parent that is not yet in the mapping table.
        // Its PartOfValue records the unresolved parent id; PartOfId stays null until the parent appears.
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location
                {
                    Id = childLocationId,
                    Name = "Bed 12",
                    ManagingOrganization = new ResourceReference(orgReference),
                    PartOf = new ResourceReference($"Location/{parentLocationId}")
                },
                cancellationToken: CancellationToken.None);
        }

        // Precondition: the child is an orphan — PartOfValue points at the parent, PartOfId is unresolved.
        using (var preScope = _fixture.ServiceProvider.CreateScope())
        {
            var preQueries = preScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
            var orphan = await preQueries.GetByFacilityIdAndLocationIdAsync(facilityId, childLocationId);
            Assert.NotNull(orphan);
            Assert.Equal(parentLocationId, orphan.PartOfValue);
            Assert.Null(orphan.PartOfId);
        }

        // The parent is queried later. Inserting it must adopt the waiting child.
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location { Id = parentLocationId, Name = "Med-Surg Unit 3", ManagingOrganization = new ResourceReference(orgReference) },
                cancellationToken: CancellationToken.None);
        }

        // The child's PartOfId is backfilled to the parent's mapping id, with PartOfValue unchanged.
        using var verifyScope = _fixture.ServiceProvider.CreateScope();
        var queries = verifyScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var parent = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, parentLocationId);
        var child = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, childLocationId);

        Assert.NotNull(parent);
        Assert.NotNull(child);
        Assert.Equal(parentLocationId, child.PartOfValue);
        Assert.Equal(parent.LocationMappingId, child.PartOfId);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_ChildQueriedAfterParent_LinksPartOfIdOnInsert()
    {
        var facilityId = NewFacilityId("ForwardLink");
        var orgReference = "Organization/org-1";
        await SeedActiveConfigAsync(facilityId, orgReference);

        var parentLocationId = NewLocationId("Parent");
        var childLocationId = NewLocationId("Child");

        // Parent first this time: when the child is inserted, the parent already exists, so the
        // PartOfId is resolved at insert rather than via the backfill adoption path.
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location { Id = parentLocationId, Name = "Med-Surg Unit 3", ManagingOrganization = new ResourceReference(orgReference) },
                cancellationToken: CancellationToken.None);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location
                {
                    Id = childLocationId,
                    Name = "Bed 12",
                    ManagingOrganization = new ResourceReference(orgReference),
                    PartOf = new ResourceReference($"Location/{parentLocationId}")
                },
                cancellationToken: CancellationToken.None);
        }

        using var verifyScope = _fixture.ServiceProvider.CreateScope();
        var queries = verifyScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var parent = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, parentLocationId);
        var child = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, childLocationId);

        Assert.NotNull(parent);
        Assert.NotNull(child);
        Assert.Equal(parentLocationId, child.PartOfValue);
        Assert.Equal(parent.LocationMappingId, child.PartOfId);
    }

    [Fact]
    public async Task UpdateLocationMappingAsync_DistinctLocations_InsertsOneRowEach()
    {
        var facilityId = NewFacilityId("MultiLoc");
        var orgReference = "Organization/org-1";
        await SeedActiveConfigAsync(facilityId, orgReference);

        var locationIdA = NewLocationId("LocA");
        var locationIdB = NewLocationId("LocB");

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location { Id = locationIdA, Name = "Location A", ManagingOrganization = new ResourceReference(orgReference) },
                cancellationToken: CancellationToken.None);
            await service.UpdateLocationMappingAsync(
                facilityId,
                new Location { Id = locationIdB, Name = "Location B", ManagingOrganization = new ResourceReference("Organization/other") },
                cancellationToken: CancellationToken.None);
        }

        using var verifyScope = _fixture.ServiceProvider.CreateScope();
        var queries = verifyScope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var rows = await queries.GetByFacilityIdAsync(facilityId);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.LocationId == locationIdA && r.IsOrgLocation);
        // Location B's managingOrganization did not match the condition, so it is not an org-location,
        // but it is still recorded in the mapping table.
        Assert.Contains(rows, r => r.LocationId == locationIdB && !r.IsOrgLocation);
    }

    [Fact]
    public async Task ReevaluateLocationMappingsAsync_WhenConditionsNowMatch_FlipsIsOrgLocationAndCascadesMappedToOrg()
    {
        var facilityId = NewFacilityId("Reeval");
        var orgReference = "Organization/org-1";
        var locationId = NewLocationId("Loc");

        // The Location body cached during a prior acquisition; its managingOrganization points at the org.
        var location = new Location
        {
            Id = locationId,
            Name = "Med-Surg",
            ManagingOrganization = new ResourceReference(orgReference)
        };

        // Arrange: a cached Location body, a mapping currently flagged NON-org (as if evaluated under
        // old conditions), and an encounter mapping linked to it that is therefore NOT mapped to org.
        int mappingId;
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
                LocationName = "Med-Surg",
                IsOrgLocation = false,
                IsActive = true
            });
            await dbContext.SaveChangesAsync();
            mappingId = mapping.LocationMappingId;

            await encounterMappingManager.CreateAsync(new CreateEncounterMappingModel
            {
                FacilityId = facilityId,
                PatientId = "P1",
                EncounterId = "E1",
                MappedToOrg = false,
                OrganizationLocationMappingIds = new List<int> { mappingId }
            });
        }

        // A condition that NOW matches the cached Location is activated.
        await SeedActiveConfigAsync(facilityId, orgReference);

        // Act — re-evaluate the facility's cached Locations against the current conditions.
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = CreateService(scope);
            await service.ReevaluateLocationMappingsAsync(facilityId, CancellationToken.None);
        }

        // Assert in a fresh scope (the cascade writes via ExecuteUpdateAsync, bypassing the tracker).
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
