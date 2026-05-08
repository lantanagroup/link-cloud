using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.Extensions.DependencyInjection;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries
{
    [Collection("DataAcquisitionIntegrationTests")]
    public class ReferenceResourcesQueriesTests
    {
        private readonly DataAcquisitionIntegrationTestFixture _fixture;

        public ReferenceResourcesQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task SeedReferenceResources(List<ReferenceResources> resources)
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.ReferenceResources.AddRange(resources);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task GetAsync_ExistingResource_ReturnsModel()
        {
            // Arrange
            var facilityId = "Facility1";
            var resourceId = "Resource1";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    ResourceId = resourceId,
                    ResourceType = "Type1",
                    ReferenceResource = "Ref1",
                    QueryPhase = QueryPhase.Initial,
                    DataAcquisitionLogId = null,
                    CreateDate = DateTime.UtcNow,
                    ModifyDate = DateTime.UtcNow
                }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();

            // Act
            var result = await queries.GetAsync(resourceId, facilityId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(resourceId, result.ResourceId);
            Assert.Equal(facilityId, result.FacilityId);
        }

        [Fact]
        public async Task GetAsync_NonExistingResource_ReturnsNull()
        {
            // Arrange
            await SeedReferenceResources(new List<ReferenceResources>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();

            // Act
            var result = await queries.GetAsync("NonExisting", "Facility1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByResourceIdsAsync_MultipleIds_ReturnsMatchingModels()
        {
            // Arrange
            var facilityId = "Facility1";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = "R1", ResourceType = "Type1", QueryPhase = QueryPhase.Initial, ReferenceResource = "Ref1" },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = "R2", ResourceType = "Type2", QueryPhase = QueryPhase.Supplemental, ReferenceResource = "Ref2" },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "Other", ResourceId = "R3", ResourceType = "Type3", QueryPhase = QueryPhase.Initial, ReferenceResource = "Ref3" }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var ids = new List<string> { "R1", "R2", "NonExisting" };

            // Act
            var results = (await queries.SearchAsync(new SearchReferenceResourcesModel
            {
                ResourceIds = ids,
                FacilityId = "Facility1"
            })).Records;

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.ResourceId == "R1");
            Assert.Contains(results, r => r.ResourceId == "R2");
        }

        [Fact]
        public async Task GetByResourceIdsAsync_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "Facility1", ResourceId = "R1", ResourceType = "Type1", QueryPhase = QueryPhase.Initial, ReferenceResource = "Ref1" }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var ids = new List<string> { "NonExisting" };

            // Act
            var results = (await queries.SearchAsync(new SearchReferenceResourcesModel
            { 
                ResourceIds = ids, 
                FacilityId = "Facility1"
            })).Records;

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchAsync_NoFilters_ReturnsAllPaged()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "R1", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial, DataAcquisitionLogId = null },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "R2", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental, DataAcquisitionLogId = null }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(2, result.Metadata.TotalCount);
            Assert.Equal(2, result.Records.Count);
        }

        [Fact]
        public async Task SearchAsync_WithFacilityIdFilter_ReturnsFiltered()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "R1", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "R2", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = "F1", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal("F1", result.Records[0].FacilityId);
        }

        [Fact]
        public async Task SearchAsync_WithResourceIdFilter_ReturnsFiltered()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "R1", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "R2", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { ResourceId = "R1", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal("R1", result.Records[0].ResourceId);
        }

        [Fact]
        public async Task SearchAsync_WithResourceTypeFilter_ReturnsFiltered()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "R1", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "R2", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { ResourceType = "T1", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal("T1", result.Records[0].ResourceType);
        }

        [Fact]
        public async Task SearchAsync_WithQueryPhaseFilter_ReturnsFiltered()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "R1", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "R2", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { QueryPhase = QueryPhase.Initial, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(QueryPhase.Initial, result.Records[0].QueryPhase);
        }

        [Fact]
        public async Task SearchAsync_WithDataAcquisitionLogIdFilter_ReturnsFiltered()
        {
            // Arrange
            using var scopeSeed = _fixture.ServiceProvider.CreateScope();
            var dbContext = scopeSeed.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var log1 = new DataAcquisitionLog
            {
                Id = 1,
                FacilityId = "F1",
                Status = RequestStatus.Pending,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };
            var log2 = new DataAcquisitionLog
            {
                Id = 2,
                FacilityId = "F2",
                Status = RequestStatus.Pending,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };

            dbContext.DataAcquisitionLogs.Add(log1);
            dbContext.DataAcquisitionLogs.Add(log2);
            await dbContext.SaveChangesAsync();

            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "R1", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial, DataAcquisitionLogId = 1 },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "R2", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental, DataAcquisitionLogId = 2 }
            };
            dbContext.ReferenceResources.AddRange(resources);
            await dbContext.SaveChangesAsync();

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { DataAcquisitionLogId = 1, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(1, result.Records[0].DataAcquisitionLogId);
        }

        [Fact]
        public async Task SearchAsync_Pagination_ReturnsCorrectPage()
        {
            // Arrange
            var resources = Enumerable.Range(1, 5).Select(i => new ReferenceResources
            {
                Id = Guid.NewGuid(),
                FacilityId = $"F{i}",
                ResourceId = $"R{i}",
                ResourceType = $"T{i}",
                ReferenceResource = $"Ref{i}",
                QueryPhase = QueryPhase.Initial
            }).ToList();
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { PageNumber = 2, PageSize = 2 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(5, result.Metadata.TotalCount);
            Assert.Equal(3, result.Metadata.TotalPages); // Ceiling(5/2) = 3
            Assert.Equal(2, result.Records.Count);
        }

        [Fact]
        public async Task SearchAsync_SortAscending_ReturnsSorted()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "B", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "A", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { SortBy = "ResourceId", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal("A", result.Records[0].ResourceId);
            Assert.Equal("B", result.Records[1].ResourceId);
        }

        [Fact]
        public async Task SearchAsync_SortDescending_ReturnsSorted()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F1", ResourceId = "A", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = "F2", ResourceId = "B", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { SortBy = "ResourceId", SortOrder = SortOrder.Descending, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal("B", result.Records[0].ResourceId);
            Assert.Equal("A", result.Records[1].ResourceId);
        }

        [Fact]
        public async Task SearchAsync_InvalidSortBy_FallsBackToDefault()
        {
            // Arrange
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = new Guid("00000000-0000-0000-0000-000000000002"), FacilityId = "F2", ResourceId = "R2", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental },
                new ReferenceResources { Id = new Guid("00000000-0000-0000-0000-000000000001"), FacilityId = "F1", ResourceId = "R1", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { SortBy = "Invalid", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            // Assuming default sort is by "id" lowercase, which sorts by Id
            Assert.Equal(resources[1].Id, result.Records[0].Id); // Lower Id first
        }

        [Fact]
        public async Task SearchAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            await SeedReferenceResources(new List<ReferenceResources>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => queries.SearchAsync(null!));
        }

        [Fact]
        public async Task SearchAsync_EmptyDatabase_ReturnsEmptyPaged()
        {
            // Arrange
            await SeedReferenceResources(new List<ReferenceResources>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(0, result.Metadata.TotalCount);
            Assert.Empty(result.Records);
        }
    }
}