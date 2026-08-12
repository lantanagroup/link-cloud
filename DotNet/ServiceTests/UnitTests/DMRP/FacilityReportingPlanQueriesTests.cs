using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    /// <summary>
    /// DMRP entities are persisted through the host service's context, so these exercise the queries
    /// against a <see cref="TenantDbContext"/>. Filters are the point: they run as SQL here rather
    /// than against a mocked repository that would accept any predicate.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class FacilityReportingPlanQueriesTests : IDisposable
    {
        private const string FacilityId = "100";
        private const string OtherFacilityId = "200";

        private readonly SqliteConnection _connection;

        public FacilityReportingPlanQueriesTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        public void Dispose() => _connection.Dispose();

        private TenantDbContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<TenantDbContext>();
            builder.UseSqlite(_connection);

            // The host registers this interceptor, and it is what stamps CreateDate and ModifyDate, so
            // the context under test needs it to behave like the running service.
            builder.AddInterceptors(new UpdateBaseEntityInterceptor());

            var context = new TenantDbContext(builder.Options);
            context.Database.EnsureCreated();
            return context;
        }

        private static FacilityReportingPlanQueries CreateQueries(TenantDbContext context) =>
            new(new EntityRepository<FacilityReportingPlan, TenantDbContext>(context));

        /// <summary>
        /// Reporting plans point at a measure mapping by foreign key, so one has to exist before any
        /// plan can be stored.
        /// </summary>
        private static MeasureMapping AddMapping(TenantDbContext context)
        {
            var mappingNumber = context.ChangeTracker.Entries<MeasureMapping>().Count() + 1;
            var mapping = new MeasureMapping
            {
                Measure = $"test-measure-{mappingNumber}",
                DQM = $"test-dqm-{mappingNumber}"
            };
            context.MeasureMappings.Add(mapping);
            return mapping;
        }

        private static FacilityReportingPlan AddPlan(TenantDbContext context, MeasureMapping mapping,
            string facilityId = FacilityId, int month = 5, int year = 2026, bool isReporting = true)
        {
            var plan = new FacilityReportingPlan
            {
                FacilityId = facilityId,
                MeasureMappingId = mapping.Id,
                ReportingMonth = month,
                ReportingYear = year,
                IsReporting = isReporting
            };

            context.FacilityReportingPlans.Add(plan);
            return plan;
        }

        [Fact]
        public async Task GetAsync_ExistingId_ReturnsEveryStoredField()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            var entity = AddPlan(context, mapping, month: 7, year: 2027, isReporting: false);
            await context.SaveChangesAsync();

            var result = await CreateQueries(context).GetAsync(entity.Id);

            Assert.NotNull(result);
            Assert.Equal(entity.Id, result!.Id);
            Assert.Equal(FacilityId, result.FacilityId);
            Assert.Equal(mapping.Id, result.MeasureMappingId);
            Assert.Equal(7, result.ReportingMonth);
            Assert.Equal(2027, result.ReportingYear);
            Assert.False(result.IsReporting);
            Assert.NotEqual(default, result.CreateDate);
        }

        [Fact]
        public async Task GetAsync_MissingId_ReturnsNull()
        {
            using var context = CreateContext();

            var result = await CreateQueries(context).GetAsync(Guid.NewGuid().ToString());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetForFacilityAsync_ReturnsOnlyThatFacilitysPlans()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping);
            AddPlan(context, mapping, facilityId: OtherFacilityId);
            await context.SaveChangesAsync();

            var results = await CreateQueries(context).GetForFacilityAsync(FacilityId);

            Assert.Single(results);
            Assert.Equal(FacilityId, results[0].FacilityId);
        }

        [Fact]
        public async Task GetForFacilityAsync_NarrowsToTheRequestedPeriod()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping, month: 5, year: 2026);
            AddPlan(context, mapping, month: 6, year: 2026);
            AddPlan(context, mapping, month: 5, year: 2025);
            await context.SaveChangesAsync();

            var results = await CreateQueries(context).GetForFacilityAsync(FacilityId, reportingMonth: 5, reportingYear: 2026);

            Assert.Single(results);
            Assert.Equal(5, results[0].ReportingMonth);
            Assert.Equal(2026, results[0].ReportingYear);
        }

        [Fact]
        public async Task GetForFacilityAsync_CanNarrowToPlansTheFacilityIsReporting()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            var otherMapping = AddMapping(context);
            AddPlan(context, mapping, isReporting: true);
            AddPlan(context, otherMapping, isReporting: false);
            await context.SaveChangesAsync();

            var reporting = await CreateQueries(context).GetForFacilityAsync(FacilityId, isReporting: true);
            var notReporting = await CreateQueries(context).GetForFacilityAsync(FacilityId, isReporting: false);
            var everything = await CreateQueries(context).GetForFacilityAsync(FacilityId);

            Assert.Single(reporting);
            Assert.Single(notReporting);

            // Unenrolled measures are kept as IsReporting = 0 rather than deleted, so an unfiltered
            // read has to return them too.
            Assert.Equal(2, everything.Count);
        }

        [Fact]
        public async Task GetForFacilityAsync_UnknownFacility_ReturnsEmpty()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping);
            await context.SaveChangesAsync();

            var results = await CreateQueries(context).GetForFacilityAsync("does-not-exist");

            Assert.Empty(results);
        }

        [Fact]
        public async Task PagedSearchAsync_NoFilters_ReturnsPagedRecordsAndMetadata()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping, month: 1);
            AddPlan(context, mapping, month: 2);
            AddPlan(context, mapping, month: 3);
            await context.SaveChangesAsync();

            var result = await CreateQueries(context).PagedSearchAsync(pageSize: 2, pageNumber: 1);

            Assert.Equal(2, result.Records.Count);
            Assert.Equal(3, result.Metadata.TotalCount);
        }

        [Fact]
        public async Task PagedSearchAsync_FiltersByFacility()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping);
            AddPlan(context, mapping, facilityId: OtherFacilityId);
            await context.SaveChangesAsync();

            var result = await CreateQueries(context).PagedSearchAsync(facilityId: OtherFacilityId);

            Assert.Single(result.Records);
            Assert.Equal(OtherFacilityId, result.Records[0].FacilityId);
        }

        [Fact]
        public async Task PagedSearchAsync_FiltersByMeasureMapping()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            var otherMapping = AddMapping(context);
            AddPlan(context, mapping);
            AddPlan(context, otherMapping);
            await context.SaveChangesAsync();

            var result = await CreateQueries(context).PagedSearchAsync(measureMappingId: otherMapping.Id);

            Assert.Single(result.Records);
            Assert.Equal(otherMapping.Id, result.Records[0].MeasureMappingId);
        }

        [Fact]
        public async Task PagedSearchAsync_CombinesFilters()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            var otherMapping = AddMapping(context);
            AddPlan(context, mapping, month: 5, year: 2026, isReporting: true);
            AddPlan(context, otherMapping, month: 5, year: 2026, isReporting: false);
            AddPlan(context, mapping, facilityId: OtherFacilityId, month: 5, year: 2026, isReporting: true);
            await context.SaveChangesAsync();

            var result = await CreateQueries(context).PagedSearchAsync(facilityId: FacilityId, reportingMonth: 5,
                reportingYear: 2026, isReporting: true);

            Assert.Single(result.Records);
            Assert.Equal(FacilityId, result.Records[0].FacilityId);
            Assert.True(result.Records[0].IsReporting);
        }

        [Fact]
        public async Task PagedSearchAsync_SortsByAReportingColumn()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping, month: 3);
            AddPlan(context, mapping, month: 1);
            AddPlan(context, mapping, month: 2);
            await context.SaveChangesAsync();

            var result = await CreateQueries(context).PagedSearchAsync(
                sortBy: nameof(FacilityReportingPlan.ReportingMonth),
                sortOrder: LantanaGroup.Link.Shared.Application.Enums.SortOrder.Ascending);

            Assert.Equal(new[] { 1, 2, 3 }, result.Records.Select(r => r.ReportingMonth));
        }

        [Fact]
        public async Task StoringTheSamePeriodTwiceForAFacilityAndMapping_IsRefusedByTheDatabase()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping, month: 5, year: 2026);
            await context.SaveChangesAsync();

            AddPlan(context, mapping, month: 5, year: 2026, isReporting: false);

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task AMeasureMappingWithReportingPlans_CannotBeDeleted()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context);
            AddPlan(context, mapping);
            await context.SaveChangesAsync();

            context.MeasureMappings.Remove(mapping);

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
