using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Microsoft.Extensions.DependencyInjection;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using DaRequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
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
            dbContext.ReferenceResources.AddRange(resources);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task GetAsync_ExistingResource_ReturnsModel()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"Facility1_{tag}";
            var resourceId = $"Resource1_{tag}";
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
            var tag = Guid.NewGuid().ToString("N");
            await SeedReferenceResources(new List<ReferenceResources>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();

            // Act
            var result = await queries.GetAsync($"NonExisting_{tag}", $"Facility1_{tag}");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByResourceIdsAsync_MultipleIds_ReturnsMatchingModels()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"Facility1_{tag}";
            var resource1 = $"R1_{tag}";
            var resource2 = $"R2_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = resource1, ResourceType = "Type1", QueryPhase = QueryPhase.Initial, ReferenceResource = "Ref1" },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = resource2, ResourceType = "Type2", QueryPhase = QueryPhase.Supplemental, ReferenceResource = "Ref2" },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"Other_{tag}", ResourceId = $"R3_{tag}", ResourceType = "Type3", QueryPhase = QueryPhase.Initial, ReferenceResource = "Ref3" }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var ids = new List<string> { resource1, resource2, $"NonExisting_{tag}" };

            // Act
            var results = (await queries.SearchAsync(new SearchReferenceResourcesModel
            {
                ResourceIds = ids,
                FacilityId = facilityId
            })).Records;

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.ResourceId == resource1);
            Assert.Contains(results, r => r.ResourceId == resource2);
        }

        [Fact]
        public async Task GetByResourceIdsAsync_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"Facility1_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = $"R1_{tag}", ResourceType = "Type1", QueryPhase = QueryPhase.Initial, ReferenceResource = "Ref1" }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var ids = new List<string> { $"NonExisting_{tag}" };

            // Act
            var results = (await queries.SearchAsync(new SearchReferenceResourcesModel
            {
                ResourceIds = ids,
                FacilityId = facilityId
            })).Records;

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchAsync_NoFilters_ReturnsAllPaged()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"F1_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = $"R1_{tag}", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = $"R2_{tag}", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = facilityId, PageNumber = 1, PageSize = 10 };

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
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"F1_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = $"R1_{tag}", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", ResourceId = $"R2_{tag}", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = facilityId, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(facilityId, result.Records[0].FacilityId);
        }

        [Fact]
        public async Task SearchAsync_WithResourceIdFilter_ReturnsFiltered()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var resourceId = $"R1_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", ResourceId = resourceId, ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", ResourceId = $"R2_{tag}", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { ResourceId = resourceId, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(resourceId, result.Records[0].ResourceId);
        }

        [Fact]
        public async Task SearchAsync_WithResourceTypeFilter_ReturnsFiltered()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var resourceType = $"T1_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", ResourceId = $"R1_{tag}", ResourceType = resourceType, ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", ResourceId = $"R2_{tag}", ResourceType = $"T2_{tag}", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { ResourceType = resourceType, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(resourceType, result.Records[0].ResourceType);
        }

        [Fact]
        public async Task SearchAsync_WithQueryPhaseFilter_ReturnsFiltered()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"Phase_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = $"R1_{tag}", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = $"R2_{tag}", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = facilityId, QueryPhase = QueryPhase.Initial, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(QueryPhase.Initial, result.Records[0].QueryPhase);
        }

        [Fact]
        public async Task SearchAsync_WithDataAcquisitionLogId_ReturnsLinkedResources()
        {
            // Arrange
            using var scopeSeed = _fixture.ServiceProvider.CreateScope();
            var dbContext = scopeSeed.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            var tag = Guid.NewGuid().ToString("N");
            var log1 = new DataAcquisitionLog
            {
                FacilityId = $"F1_{tag}",
                Status = DaRequestStatus.Pending,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };
            var log2 = new DataAcquisitionLog
            {
                FacilityId = $"F2_{tag}",
                Status = DaRequestStatus.Pending,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };

            dbContext.DataAcquisitionLogs.Add(log1);
            dbContext.DataAcquisitionLogs.Add(log2);
            await dbContext.SaveChangesAsync();

            var ref1 = new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", ResourceId = $"R1_{tag}", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial };
            var ref2 = new ReferenceResources { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", ResourceId = $"R2_{tag}", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental };
            dbContext.ReferenceResources.AddRange(ref1, ref2);
            await dbContext.SaveChangesAsync();

            // Link via skip-navigation
            log1.ReferenceResources.Add(ref1);
            log2.ReferenceResources.Add(ref2);
            await dbContext.SaveChangesAsync();

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();

            // Act
            var result = await queries.SearchAsync(new SearchReferenceResourcesModel
            {
                DataAcquisitionLogId = log1.Id,
                PageNumber = 1,
                PageSize = 10
            });

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(ref1.Id, result.Records[0].Id);
        }

        [Fact]
        public async Task SearchAsync_Pagination_ReturnsCorrectPage()
        {
            // Arrange
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"Paging_{tag}";
            var resources = Enumerable.Range(1, 5).Select(i => new ReferenceResources
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                ResourceId = $"R{i}_{tag}",
                ResourceType = $"T{i}",
                ReferenceResource = $"Ref{i}",
                QueryPhase = QueryPhase.Initial
            }).ToList();
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = facilityId, PageNumber = 2, PageSize = 2 };

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
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"SortAsc_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = "B", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = "A", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = facilityId, SortBy = "ResourceId", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

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
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"SortDesc_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = "A", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial },
                new ReferenceResources { Id = Guid.NewGuid(), FacilityId = facilityId, ResourceId = "B", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = facilityId, SortBy = "ResourceId", SortOrder = SortOrder.Descending, PageNumber = 1, PageSize = 10 };

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
            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"SortDefault_{tag}";
            var resources = new List<ReferenceResources>
            {
                new ReferenceResources { Id = new Guid("00000000-0000-0000-0000-000000000002"), FacilityId = facilityId, ResourceId = $"R2_{tag}", ResourceType = "T2", ReferenceResource = "Ref2", QueryPhase = QueryPhase.Supplemental },
                new ReferenceResources { Id = new Guid("00000000-0000-0000-0000-000000000001"), FacilityId = facilityId, ResourceId = $"R1_{tag}", ResourceType = "T1", ReferenceResource = "Ref1", QueryPhase = QueryPhase.Initial }
            };
            await SeedReferenceResources(resources);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = facilityId, SortBy = "Invalid", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

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
            var tag = Guid.NewGuid().ToString("N");
            await SeedReferenceResources(new List<ReferenceResources>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var request = new SearchReferenceResourcesModel { FacilityId = $"NoData_{tag}", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(0, result.Metadata.TotalCount);
            Assert.Empty(result.Records);
        }
    }
}
