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
            dbContext.QueryPlans.AddRange(plans);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task GetAsync_ExistingPlan_ReturnsModel()
        {
            var facilityId = $"GetExisting_{Guid.NewGuid():N}";
            var type = Frequency.Daily;
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(), FacilityId = facilityId, Type = type,
                    PlanName = "Plan1", EHRDescription = "Desc1", LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow, ModifyDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            var result = await queries.GetAsync(facilityId, type);

            Assert.NotNull(result);
            Assert.Equal(facilityId, result.FacilityId);
            Assert.Equal(type, result.Type);
        }

        [Fact]
        public async Task GetAsync_NonExistingPlan_ReturnsNull()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            var result = await queries.GetAsync($"NonExisting_{Guid.NewGuid():N}", Frequency.Daily);

            Assert.Null(result);
        }

        [Fact]
        public async Task FindAsync_MatchingPredicate_ReturnsModels()
        {
            var facilityId = $"FindMatch_{Guid.NewGuid():N}";
            var plans = new List<QueryPlan>
            {
                new QueryPlan
                {
                    Id = Guid.NewGuid(), FacilityId = facilityId, Type = Frequency.Daily,
                    PlanName = "Plan1", EHRDescription = "Desc1", LookBack = "1d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                },
                new QueryPlan
                {
                    Id = Guid.NewGuid(), FacilityId = $"Other_{Guid.NewGuid():N}", Type = Frequency.Weekly,
                    PlanName = "Plan2", EHRDescription = "Desc2", LookBack = "2d",
                    InitialQueries = new Dictionary<string, IQueryConfig>(),
                    SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                    CreateDate = DateTime.UtcNow
                }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            Expression<Func<QueryPlan, bool>> predicate = q => q.FacilityId == facilityId;

            var results = await queries.FindAsync(predicate);

            Assert.Single(results);
            Assert.Equal(facilityId, results[0].FacilityId);
        }

        [Fact]
        public async Task FindAsync_NoMatches_ReturnsEmptyList()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var uniqueFacility = $"NoMatch_{Guid.NewGuid():N}";
            Expression<Func<QueryPlan, bool>> predicate = q => q.FacilityId == uniqueFacility;

            var results = await queries.FindAsync(predicate);

            Assert.Empty(results);
        }

        [Fact]
        public async Task GetPlanNamesAsync_MultiplePlans_ReturnsDistinctNames()
        {
            var facilityId = $"PlanNames_{Guid.NewGuid():N}";
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = facilityId, PlanName = "Plan1", Type = Frequency.Daily, EHRDescription = "Desc1", LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = facilityId, PlanName = "Plan1", Type = Frequency.Weekly, EHRDescription = "Desc1", LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = facilityId, PlanName = "Plan2", Type = Frequency.Monthly, EHRDescription = "Desc2", LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            var names = await queries.GetPlanNamesAsync(facilityId);

            Assert.Equal(2, names.Count);
            Assert.Contains("Plan1", names);
            Assert.Contains("Plan2", names);
        }

        [Fact]
        public async Task GetPlanNamesAsync_NoPlans_ReturnsEmptyList()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            var names = await queries.GetPlanNamesAsync($"NoPlans_{Guid.NewGuid():N}");

            Assert.Empty(names);
        }

        [Fact]
        public async Task SearchAsync_NoFilters_ReturnsAllPaged()
        {
            var tag = Guid.NewGuid().ToString("N");
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", PlanName = "P1", Type = Frequency.Daily, EHRDescription = tag, LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = "P2", Type = Frequency.Weekly, EHRDescription = tag, LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = tag, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(2, result.Metadata.TotalCount);
            Assert.Equal(2, result.Records.Count);
        }

        [Fact]
        public async Task SearchAsync_WithFacilityIdFilter_ReturnsFiltered()
        {
            var tag = Guid.NewGuid().ToString("N");
            var f1 = $"F1_{tag}";
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = f1, PlanName = "P1", Type = Frequency.Daily, EHRDescription = "D1", LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = "P2", Type = Frequency.Weekly, EHRDescription = "D2", LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { FacilityId = f1, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(f1, result.Records[0].FacilityId);
        }

        [Fact]
        public async Task SearchAsync_WithPlanNameFilter_ReturnsFiltered()
        {
            var tag = Guid.NewGuid().ToString("N");
            var planName = $"P1_{tag}";
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", PlanName = planName, Type = Frequency.Daily, EHRDescription = "D1", LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = $"P2_{tag}", Type = Frequency.Weekly, EHRDescription = "D2", LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { PlanName = planName, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(planName, result.Records[0].PlanName);
        }

        [Fact]
        public async Task SearchAsync_WithTypeFilter_ReturnsFiltered()
        {
            var tag = Guid.NewGuid().ToString("N");
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", PlanName = "P1", Type = Frequency.Daily, EHRDescription = tag, LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = "P2", Type = Frequency.Weekly, EHRDescription = tag, LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = tag, Type = Frequency.Daily, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(Frequency.Daily, result.Records[0].Type);
        }

        [Fact]
        public async Task SearchAsync_WithEHRDescriptionFilter_ReturnsFiltered()
        {
            var tag = Guid.NewGuid().ToString("N");
            var desc = $"Desc1_{tag}";
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", PlanName = "P1", Type = Frequency.Daily, EHRDescription = desc, LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = "P2", Type = Frequency.Weekly, EHRDescription = $"Desc2_{tag}", LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = desc, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(desc, result.Records[0].EHRDescription);
        }

        [Fact]
        public async Task SearchAsync_WithLookBackFilter_ReturnsFiltered()
        {
            var tag = Guid.NewGuid().ToString("N");
            var lookBack = $"1d_{tag}";
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", PlanName = "P1", Type = Frequency.Daily, EHRDescription = tag, LookBack = lookBack, InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = "P2", Type = Frequency.Weekly, EHRDescription = tag, LookBack = $"2d_{tag}", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { LookBack = lookBack, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(1, result.Metadata.TotalCount);
            Assert.Equal(lookBack, result.Records[0].LookBack);
        }

        [Fact]
        public async Task SearchAsync_Pagination_ReturnsCorrectPage()
        {
            var tag = Guid.NewGuid().ToString("N");
            var plans = Enumerable.Range(1, 5).Select(i => new QueryPlan
            {
                Id = Guid.NewGuid(), FacilityId = $"F{i}_{tag}", PlanName = $"P{i}",
                Type = Frequency.Daily, EHRDescription = tag, LookBack = $"{i}d",
                InitialQueries = new Dictionary<string, IQueryConfig>(),
                SupplementalQueries = new Dictionary<string, IQueryConfig>(),
                CreateDate = DateTime.UtcNow
            }).ToList();
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = tag, PageNumber = 2, PageSize = 2 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(5, result.Metadata.TotalCount);
            Assert.Equal(3, result.Metadata.TotalPages);
            Assert.Equal(2, result.Records.Count);
        }

        [Fact]
        public async Task SearchAsync_SortAscending_ReturnsSorted()
        {
            var tag = Guid.NewGuid().ToString("N");
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", PlanName = $"B_{tag}", Type = Frequency.Daily, EHRDescription = tag, LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = $"A_{tag}", Type = Frequency.Weekly, EHRDescription = tag, LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = tag, SortBy = "PlanName", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal($"A_{tag}", result.Records[0].PlanName);
            Assert.Equal($"B_{tag}", result.Records[1].PlanName);
        }

        [Fact]
        public async Task SearchAsync_SortDescending_ReturnsSorted()
        {
            var tag = Guid.NewGuid().ToString("N");
            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F1_{tag}", PlanName = $"A_{tag}", Type = Frequency.Daily, EHRDescription = tag, LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = Guid.NewGuid(), FacilityId = $"F2_{tag}", PlanName = $"B_{tag}", Type = Frequency.Weekly, EHRDescription = tag, LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = tag, SortBy = "PlanName", SortOrder = SortOrder.Descending, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal($"B_{tag}", result.Records[0].PlanName);
            Assert.Equal($"A_{tag}", result.Records[1].PlanName);
        }

        [Fact]
        public async Task SearchAsync_InvalidSortBy_FallsBackToDefault()
        {
            var tag = Guid.NewGuid().ToString("N");
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            var plans = new List<QueryPlan>
            {
                new QueryPlan { Id = id2, FacilityId = $"F2_{tag}", PlanName = "P2", Type = Frequency.Weekly, EHRDescription = tag, LookBack = "2d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow },
                new QueryPlan { Id = id1, FacilityId = $"F1_{tag}", PlanName = "P1", Type = Frequency.Daily, EHRDescription = tag, LookBack = "1d", InitialQueries = new Dictionary<string, IQueryConfig>(), SupplementalQueries = new Dictionary<string, IQueryConfig>(), CreateDate = DateTime.UtcNow }
            };
            await SeedQueryPlans(plans);

            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { EHRDescription = tag, SortBy = "Invalid", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };
            var expectedRequest = new SearchQueryPlanModel { EHRDescription = tag, SortBy = "Id", SortOrder = SortOrder.Ascending, PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);
            var expected = await queries.SearchAsync(expectedRequest);

            Assert.Equal(expected.Records.Select(r => r.Id), result.Records.Select(r => r.Id));
        }

        [Fact]
        public async Task SearchAsync_NullRequest_ThrowsArgumentNullException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => queries.SearchAsync(null!));
        }

        [Fact]
        public async Task SearchAsync_EmptyDatabase_ReturnsEmptyPaged()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var queries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var request = new SearchQueryPlanModel { FacilityId = $"Empty_{Guid.NewGuid():N}", PageNumber = 1, PageSize = 10 };

            var result = await queries.SearchAsync(request);

            Assert.Equal(0, result.Metadata.TotalCount);
            Assert.Empty(result.Records);
        }
    }
}
