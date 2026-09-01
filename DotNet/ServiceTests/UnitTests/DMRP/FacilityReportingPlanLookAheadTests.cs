using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    /// <summary>
    /// The look-ahead answers from what DMRP recorded and projects the rest, so these run the real
    /// queries, source and projector against a <see cref="TenantDbContext"/> - the seams between them
    /// are what the behaviour depends on.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class FacilityReportingPlanLookAheadTests : IDisposable
    {
        private const string FacilityId = "100";

        /// <summary>October 2026, so a six-month window runs into the following year.</summary>
        private static readonly ReportingPeriod Anchor = new(2026, 10);

        private readonly SqliteConnection _connection;

        public FacilityReportingPlanLookAheadTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        public void Dispose() => _connection.Dispose();

        private TenantDbContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<TenantDbContext>();
            builder.UseSqlite(_connection);
            builder.AddInterceptors(new UpdateBaseEntityInterceptor());

            var context = new TenantDbContext(builder.Options);
            context.Database.EnsureCreated();
            return context;
        }

        private static FacilityReportingPlanLookAhead CreateLookAhead(TenantDbContext context)
        {
            var queries = new FacilityReportingPlanQueries(
                new EntityRepository<FacilityReportingPlan, TenantDbContext>(context),
                new EntityRepository<MeasureMapping, TenantDbContext>(context));

            var source = new DbBackedReportingPlanSource(
                NullLogger<DbBackedReportingPlanSource>.Instance,
                new EntityRepository<FacilityReportingPlan, TenantDbContext>(context),
                new EntityRepository<MeasureMapping, TenantDbContext>(context));

            return new FacilityReportingPlanLookAhead(queries, source,
                new ReportingPlanScheduleProjector(NullLogger<ReportingPlanScheduleProjector>.Instance));
        }

        private static MeasureMapping AddMapping(TenantDbContext context, string measure,
            Frequency frequency = Frequency.Monthly, string? dqm = null)
        {
            var mapping = new MeasureMapping
            {
                Measure = measure,
                DQM = dqm ?? $"dqm-{measure}",
                Frequency = frequency
            };

            context.MeasureMappings.Add(mapping);
            return mapping;
        }

        private static void AddPlan(TenantDbContext context, MeasureMapping mapping, ReportingPeriod period,
            bool isReporting = true) =>
            context.FacilityReportingPlans.Add(new FacilityReportingPlan
            {
                FacilityId = FacilityId,
                MeasureMappingId = mapping.Id,
                Measure = mapping.Measure,
                ReportingMonth = period.Month,
                ReportingYear = period.Year,
                IsReporting = isReporting
            });

        private static ReportingPeriodRange SixMonths() => ReportingPeriodRange.LookAhead(Anchor, 6);

        [Fact]
        public async Task RecordedPeriod_CarriesItsMeasuresAndTheScheduleTheyProduce()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB", Frequency.Monthly), Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, null, Anchor);

            var period = Assert.Single(page.Records);
            Assert.False(period.IsProjected);
            Assert.Equal("HOB", Assert.Single(period.Measures).Measure);
            Assert.Equal(["dqm-HOB"], period.Schedule.Monthly);
            Assert.Empty(period.Schedule.Daily);
        }

        [Fact]
        public async Task WithdrawnMeasure_IsListedButSchedulesNothing()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB"), Anchor, isReporting: false);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, null, Anchor);

            // The row is history the facility can see; it is not an obligation Link will run.
            var period = Assert.Single(page.Records);
            Assert.False(Assert.Single(period.Measures).IsReporting);
            Assert.Empty(period.Schedule.Monthly);
        }

        [Fact]
        public async Task MeasureWithNoDqm_IsListedButSchedulesNothing()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB", dqm: ""), Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, null, Anchor);

            // A measure Link cannot evaluate is the reason a report is missing, so the facility sees
            // the enrollment even though nothing is scheduled for it.
            var period = Assert.Single(page.Records);
            Assert.Equal("HOB", Assert.Single(period.Measures).Measure);
            Assert.Empty(period.Schedule.Monthly);
        }

        [Fact]
        public async Task MeasureOnAFrequencyLinkCannotTime_IsListedButSchedulesNothing()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB", Frequency.Adhoc), Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, null, Anchor);

            var period = Assert.Single(page.Records);
            Assert.Single(period.Measures);
            Assert.Empty(period.Schedule.Daily);
            Assert.Empty(period.Schedule.Weekly);
            Assert.Empty(period.Schedule.Monthly);
        }

        [Fact]
        public async Task MonthsWithNoPlanOnRecord_AreProjectedFromTheCurrentEnrollment()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB", Frequency.Weekly), Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, SixMonths(), Anchor);

            // Nothing writes rows for months that have not happened, but the frequency already says
            // the measure recurs - so the window is filled rather than left empty.
            Assert.Equal(6, page.Records.Count);
            Assert.Equal(6, page.Metadata.TotalCount);

            Assert.False(page.Records[0].IsProjected);
            Assert.All(page.Records.Skip(1), period =>
            {
                Assert.True(period.IsProjected);
                Assert.Equal("HOB", Assert.Single(period.Measures).Measure);
                Assert.Equal(["dqm-HOB"], period.Schedule.Weekly);
            });

            // Chronological, and across the year boundary.
            Assert.Equal((2026, 10), (page.Records[0].ReportingYear, page.Records[0].ReportingMonth));
            Assert.Equal((2027, 3), (page.Records[5].ReportingYear, page.Records[5].ReportingMonth));
        }

        [Fact]
        public async Task ARecordedPeriodWinsOverTheProjection()
        {
            using var context = CreateContext();
            var hob = AddMapping(context, "HOB");
            var cauti = AddMapping(context, "CAUTI");

            AddPlan(context, hob, Anchor);
            AddPlan(context, cauti, new ReportingPeriod(2026, 12));
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, SixMonths(), Anchor);

            var december = page.Records.Single(p => p.ReportingYear == 2026 && p.ReportingMonth == 12);

            // December has its own plan, so it is reported as recorded even though the projection
            // would have said HOB.
            Assert.False(december.IsProjected);
            Assert.Equal("CAUTI", Assert.Single(december.Measures).Measure);
        }

        [Fact]
        public async Task NoWindow_ProjectsNothing()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB"), Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, null, Anchor);

            // Without a bounded window there is nothing to project into.
            Assert.Single(page.Records);
            Assert.False(page.Records[0].IsProjected);
        }

        [Fact]
        public async Task NothingEnrolledAtTheAnchor_LeavesTheWindowEmpty()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB"), new ReportingPeriod(2026, 12));
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, SixMonths(), Anchor);

            // Nothing to project from, so the gaps stay absent. An empty period would read as "you
            // report nothing that month" rather than "nobody has said".
            Assert.Equal(12, Assert.Single(page.Records).ReportingMonth);
        }

        [Fact]
        public async Task AskingForWithdrawals_ProjectsNothing()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB"), Anchor, isReporting: false);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, SixMonths(), Anchor, isReporting: false);

            // A withdrawal is a fact about a period that happened; carrying it forward would report
            // the facility as not reporting in months nobody has said anything about.
            Assert.Single(page.Records);
            Assert.False(page.Records[0].IsProjected);
        }

        [Fact]
        public async Task MeasuresWithinAPeriodAreOrderedByMeasure()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "ZULU"), Anchor);
            AddPlan(context, AddMapping(context, "ALPHA"), Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, null, Anchor);

            // The contract promises a stable order rather than whatever the rows came back in.
            Assert.Equal(["ALPHA", "ZULU"], Assert.Single(page.Records).Measures.Select(m => m.Measure));
        }

        [Fact]
        public async Task AMeasureWhoseMappingDidNotResolve_IsStillListed()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context, "HOB");
            AddPlan(context, mapping, Anchor);
            await context.SaveChangesAsync();

            // Deleting the mapping out from under the plan is what a read racing a delete sees. It has
            // to go around the change tracker: removing the principal there severs the relationship
            // first, which nulls the plan's required foreign key before the delete ever runs.
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM MeasureMappings WHERE Id = {0}", mapping.Id);
            context.ChangeTracker.Clear();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, null, Anchor);

            var measure = Assert.Single(Assert.Single(page.Records).Measures);
            Assert.Equal("HOB", measure.Measure);
            Assert.Null(measure.DQM);
        }

        [Fact]
        public async Task PagesOverPeriods()
        {
            using var context = CreateContext();
            AddPlan(context, AddMapping(context, "HOB"), Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context)
                .GetAsync(FacilityId, SixMonths(), Anchor, pageSize: 4, pageNumber: 2);

            // Six periods at four to a page: the second page holds the last two.
            Assert.Equal(2, page.Records.Count);
            Assert.Equal(6, page.Metadata.TotalCount);
            Assert.Equal(2, page.Metadata.TotalPages);
        }

        [Fact]
        public async Task UnknownFacility_ReturnsAnEmptyPage()
        {
            using var context = CreateContext();

            var page = await CreateLookAhead(context).GetAsync("no-such-facility", SixMonths(), Anchor);

            Assert.Empty(page.Records);
            Assert.Equal(0, page.Metadata.TotalCount);
        }
    }
}
