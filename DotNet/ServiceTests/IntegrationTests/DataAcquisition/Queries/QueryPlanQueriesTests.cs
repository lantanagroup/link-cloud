using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries
{
    [Collection("DataAcquisitionIntegrationTests")]
    public class QueryPlanQueriesTests
    {
        private readonly DataAcquisitionIntegrationTestFixture _fixture;

        public QueryPlanQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task SeedQueryPlans(List<QueryPlan> plans)
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.QueryPlan.AddRange(plans);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task GetAsync_ExistingPlan_ReturnsModel()
        {
            // Arrange
            var facilityId = "Facility1";
            var type = Frequency.Daily;
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    Type = type,
                    PlanName = "Plan1",
                    EHRDescription = "Desc1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow,
                    ModifyDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            // Act
            var result = await queries.GetAsync(facilityId, type);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(facilityId, result.FacilityId);
            Assert.Equal(type, result.Type);
        }

        [Fact]
        public async Task GetAsync_NonExistingPlan_ReturnsNull()
        {
            // Arrange
            await SeedQueryPlans(new List<QueryPlan>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            // Act
            var result = await queries.GetAsync("NonExisting", Frequency.Daily);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task FindAsync_MatchingPredicate_ReturnsModels()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "Facility1",
                    Type = Frequency.Daily,
                    PlanName = "Plan1",
                    EHRDescription = "Desc1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "Facility2",
                    Type = Frequency.Weekly,
                    PlanName = "Plan2",
                    EHRDescription = "Desc2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            Expression<Func<QueryPlan, bool>> predicate = q => q.FacilityId == "Facility1";

            // Act
            var results = await queries.FindAsync(predicate);

            // Assert
            Assert.Single(results);
            Assert.Equal("Facility1", results[0].FacilityId);
        }

        [Fact]
        public async Task FindAsync_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "Facility1",
                    Type = Frequency.Daily,
                    PlanName = "Plan1",
                    EHRDescription = "Desc1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            Expression<Func<QueryPlan, bool>> predicate = q => q.FacilityId == "NonExisting";

            // Act
            var results = await queries.FindAsync(predicate);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task GetPlanNamesAsync_MultiplePlans_ReturnsDistinctNames()
        {
            // Arrange
            var facilityId = "Facility1";
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    PlanName = "Plan1",
                    Type = Frequency.Daily,
                    EHRDescription = "Desc1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    PlanName = "Plan1",
                    Type = Frequency.Weekly,
                    EHRDescription = "Desc1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    PlanName = "Plan2",
                    Type = Frequency.Monthly,
                    EHRDescription = "Desc2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            // Act
            var names = await queries.GetPlanNamesAsync(facilityId);

            // Assert
            Assert.Equal(2, names.Count);
            Assert.Contains("Plan1", names);
            Assert.Contains("Plan2", names);
        }

        [Fact]
        public async Task GetPlanNamesAsync_NoPlans_ReturnsEmptyList()
        {
            // Arrange
            await SeedQueryPlans(new List<QueryPlan>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            // Act
            var names = await queries.GetPlanNamesAsync("Facility1");

            // Assert
            Assert.Empty(names);
        }

        [Fact]
        public async Task SearchAsync_NoFilters_ReturnsAllPaged()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "P1",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "P2",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { PageNumber = 1, PageSize = 10 };

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
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "P1",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "P2",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { FacilityId = "F1", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal("F1", result.Records[0].FacilityId);
        }

        [Fact]
        public async Task SearchAsync_WithPlanNameFilter_ReturnsFiltered()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "P1",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "P2",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { PlanName = "P1", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal("P1", result.Records[0].PlanName);
        }

        [Fact]
        public async Task SearchAsync_WithTypeFilter_ReturnsFiltered()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "P1",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "P2",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { Type = Frequency.Daily, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(Frequency.Daily, result.Records[0].Type);
        }

        [Fact]
        public async Task SearchAsync_WithEHRDescriptionFilter_ReturnsFiltered()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "P1",
                    Type = Frequency.Daily,
                    EHRDescription = "Desc1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "P2",
                    Type = Frequency.Weekly,
                    EHRDescription = "Desc2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = "Desc1", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal("Desc1", result.Records[0].EHRDescription);
        }

        [Fact]
        public async Task SearchAsync_WithLookBackFilter_ReturnsFiltered()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "P1",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "P2",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { LookBack = "1d", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal("1d", result.Records[0].LookBack);
        }

        [Fact]
        public async Task SearchAsync_Pagination_ReturnsCorrectPage()
        {
            // Arrange
            var plans = Enumerable.Range(1, 5).Select(i => new QueryPlan
            {
                Id = Guid.NewGuid(),
                FacilityId = $"F{i}",
                PlanName = $"P{i}",
                Type = Frequency.Daily,
                EHRDescription = $"D{i}",
                LookBack = $"{i}d",
                InitialQueries = new Dictionary<string, IQueryConfig>(),
                SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                CreateDate = DateTime.UtcNow
            }).ToList();
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { PageNumber = 2, PageSize = 2 };

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
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "B",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "A",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { SortBy = "PlanName", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal("A", result.Records[0].PlanName);
            Assert.Equal("B", result.Records[1].PlanName);
        }

        [Fact]
        public async Task SearchAsync_SortDescending_ReturnsSorted()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F1",
                    PlanName = "A",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "F2",
                    PlanName = "B",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { SortBy = "PlanName", SortOrder = SortOrder.Descending, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal("B", result.Records[0].PlanName);
            Assert.Equal("A", result.Records[1].PlanName);
        }

        [Fact]
        public async Task SearchAsync_InvalidSortBy_FallsBackToDefault()
        {
            // Arrange
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = new Guid("00000000-0000-0000-0000-000000000002"),
                    FacilityId = "F2",
                    PlanName = "P2",
                    Type = Frequency.Weekly,
                    EHRDescription = "D2",
                    LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = new Guid("00000000-0000-0000-0000-000000000001"),
                    FacilityId = "F1",
                    PlanName = "P1",
                    Type = Frequency.Daily,
                    EHRDescription = "D1",
                    LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { SortBy = "Invalid", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            // Assuming default sort is by "id" lowercase, which sorts by Id
            Assert.Equal(plans[1].Id, result.Records[0].Id); // Lower Id first
        }

        [Fact]
        public async Task SearchAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            await SeedQueryPlans(new List<QueryPlan>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => queries.SearchAsync(null!));
        }

        [Fact]
        public async Task SearchAsync_EmptyDatabase_ReturnsEmptyPaged()
        {
            // Arrange
            await SeedQueryPlans(new List<QueryPlan>());

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await queries.SearchAsync(request);

            // Assert
            Assert.Equal(0, result.Metadata.TotalCount);
            Assert.Empty(result.Records);
        }
    }
}