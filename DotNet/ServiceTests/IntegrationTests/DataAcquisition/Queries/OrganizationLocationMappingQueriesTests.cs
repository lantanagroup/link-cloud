using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationMappingQueriesTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationMappingQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task GetByIdAsync_Exists_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Test-Fac");
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = "Loc-001",
            LocationName = "Test Bed",
            IsOrgLocation = true
        });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var result = await queries.GetByIdAsync(created.LocationMappingId);

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal("Loc-001", result.LocationId);
        Assert.Equal("Test Bed", result.LocationName);
    }

    [Fact]
    public async Task GetByFacilityIdAndLocationIdAsync_Exists_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Fac-123");
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = "Loc-XYZ"
        });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var result = await queries.GetByFacilityIdAndLocationIdAsync(facilityId, "Loc-XYZ");

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_ReturnsAllForFacility()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Fac-ABC");
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "Loc-1" });
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "Loc-2" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var results = await queries.GetByFacilityIdAsync(facilityId);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_ReturnsCorrectResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Fac-Filter");
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "Loc-A", IsOrgLocation = true });
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = "Loc-B", IsOrgLocation = false });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();

        var result = await queries.SearchAsync(new OrganizationLocationMappingSearchModel
        {
            FacilityId = facilityId,
            IsOrgLocation = true
        });

        Assert.Single(result.Records);
        Assert.True(result.Records[0].IsOrgLocation);
    }

    [Fact]
    public async Task SearchAsync_Pagination_WorksCorrectly()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Pag-Test");
        for (int i = 1; i <= 15; i++)
            await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = facilityId, LocationId = $"Loc-{i}" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var result = await queries.SearchAsync(new OrganizationLocationMappingSearchModel { FacilityId = facilityId }, pageNumber: 2, pageSize: 5);

        Assert.Equal(5, result.Records.Count);
        Assert.Equal(15, result.Metadata.TotalCount);
    }

    /// <summary>
    /// Creates a realistic, multi-branch healthcare location hierarchy (4 levels deep, 13 locations)
    /// with branching, multiple roots, and names chosen to exercise alphabetical child sorting.
    /// </summary>
    private async Task SetupRobustHierarchyTestDataAsync(IOrganizationLocationMappingManager manager, string facilityId)
    {
        var data = new List<(string LocationId, string LocationName, string? PartOfValue)>
        {
            ("ORG-ROOT", "Hospital Main", null),
            ("BLDG-A", "Building A", "ORG-ROOT"),
            ("BLDG-B", "Building B", "ORG-ROOT"),
            ("CLINIC-OUT", "Outpatient Clinic", "ORG-ROOT"),
            ("FLR-A1", "Floor 1-A", "BLDG-A"),
            ("FLR-A2", "Floor 2-A", "BLDG-A"),
            ("FLR-B1", "Floor 1-B", "BLDG-B"),
            ("WARD-A11", "Ward A1-1", "FLR-A1"),
            ("WARD-A12", "Ward A1-2", "FLR-A1"),
            ("WARD-A21", "Ward A2-1", "FLR-A2"),
            ("WARD-B11", "Ward B1-1", "FLR-B1"),
            ("STANDALONE-1", "Standalone Location", null),
            ("STANDALONE-2", "Child of Standalone", "STANDALONE-1")
        };

        foreach (var (locId, name, partOf) in data)
        {
            await manager.CreateAsync(new CreateOrganizationLocationMappingModel
            {
                FacilityId = facilityId,
                LocationId = locId,
                LocationName = name,
                PartOfValue = partOf,
                IsOrgLocation = true,
                IsActive = true
            });
        }
    }

    [Fact]
    public async Task GetHierarchyUpAsync_FromDeepLeaf_ReturnsCorrectPathWithProperDepths()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Hierarchy-Test-Fac");
        await SetupRobustHierarchyTestDataAsync(manager, facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var path = await queries.GetHierarchyUpAsync(facilityId, "WARD-A11");

        Assert.Equal(4, path.Count);

        Assert.Equal("ORG-ROOT", path[0].Mapping.LocationId);
        Assert.Equal("BLDG-A", path[1].Mapping.LocationId);
        Assert.Equal("FLR-A1", path[2].Mapping.LocationId);
        Assert.Equal("WARD-A11", path[3].Mapping.LocationId);

        for (int i = 0; i < path.Count; i++)
            Assert.Equal(i, path[i].Depth);
    }

    [Fact]
    public async Task GetHierarchyUpAsync_FromRoot_ReturnsSingleRootNode()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Hierarchy-Test-Fac");
        await SetupRobustHierarchyTestDataAsync(manager, facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var path = await queries.GetHierarchyUpAsync(facilityId, "ORG-ROOT");

        Assert.Single(path);
        Assert.Equal(0, path[0].Depth);
        Assert.Equal("ORG-ROOT", path[0].Mapping.LocationId);
        Assert.True(path[0].IsRoot);
    }

    [Fact]
    public async Task GetHierarchyUpAsync_NonExistentLocation_ReturnsEmptyList()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Hierarchy-Test-Fac");
        await SetupRobustHierarchyTestDataAsync(manager, facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var path = await queries.GetHierarchyUpAsync(facilityId, "NON-EXISTENT");

        Assert.Empty(path);
    }

    [Fact]
    public async Task GetFullSubtreeAsync_FromRoot_ReturnsCompleteTreeWithChildrenSortedByName()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Hierarchy-Test-Fac");
        await SetupRobustHierarchyTestDataAsync(manager, facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var tree = await queries.GetFullSubtreeAsync(facilityId, "ORG-ROOT");

        Assert.NotNull(tree);
        Assert.Equal("ORG-ROOT", tree.Mapping.LocationId);
        Assert.Equal(0, tree.Depth);

        // Root children sorted by LocationName: "Building A", "Building B", "Outpatient Clinic"
        Assert.Equal(3, tree.Children.Count);
        Assert.Equal("BLDG-A", tree.Children[0].Mapping.LocationId);
        Assert.Equal("BLDG-B", tree.Children[1].Mapping.LocationId);
        Assert.Equal("CLINIC-OUT", tree.Children[2].Mapping.LocationId);

        // Deeper verification
        var buildingA = tree.Children[0];
        Assert.Equal(2, buildingA.Children.Count);
        Assert.Equal("FLR-A1", buildingA.Children[0].Mapping.LocationId); // Floor 1-A before Floor 2-A
    }

    [Fact]
    public async Task GetFullSubtreeAsync_FromMidLevelChild_ReturnsFullTreeFromUltimateRoot()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Hierarchy-Test-Fac");
        await SetupRobustHierarchyTestDataAsync(manager, facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var tree = await queries.GetFullSubtreeAsync(facilityId, "WARD-A11");

        Assert.NotNull(tree);
        Assert.Equal("ORG-ROOT", tree.Mapping.LocationId); // Always returns from true root

        var wardNode = FindNode(tree, "WARD-A11");
        Assert.NotNull(wardNode);
        Assert.Equal(3, wardNode.Depth); // Root(0) → BLDG-A(1) → FLR-A1(2) → WARD-A11(3)
    }

    [Fact]
    public async Task GetFullSubtreeAsync_FromStandaloneRoot_ReturnsIsolatedTree()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Hierarchy-Test-Fac");
        await SetupRobustHierarchyTestDataAsync(manager, facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var tree = await queries.GetFullSubtreeAsync(facilityId, "STANDALONE-1");

        Assert.NotNull(tree);
        Assert.Equal("STANDALONE-1", tree.Mapping.LocationId);
        Assert.Single(tree.Children); // Contains STANDALONE-2
        Assert.Equal("STANDALONE-2", tree.Children[0].Mapping.LocationId);
    }

    [Fact]
    public async Task GetFullSubtreeAsync_NonExistentLocation_ReturnsNull()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = NewFacilityId("Hierarchy-Test-Fac");
        await SetupRobustHierarchyTestDataAsync(manager, facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var result = await queries.GetFullSubtreeAsync(facilityId, "NONEXISTENT-XYZ");

        Assert.Null(result);
    }

    /// <summary>
    /// Recursive helper to locate a node anywhere in the returned tree (used for mid-level assertions).
    /// </summary>
    private LocationHierarchyNode? FindNode(LocationHierarchyNode node, string locationId)
    {
        if (node.Mapping.LocationId == locationId)
            return node;

        foreach (var child in node.Children)
        {
            var found = FindNode(child, locationId);
            if (found != null) return found;
        }
        return null;
    }
}
