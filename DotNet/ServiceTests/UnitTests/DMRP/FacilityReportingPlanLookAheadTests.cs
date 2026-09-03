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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

        private static FacilityReportingPlanLookAhead CreateLookAhead(TenantDbContext context,
            IReportingPlanScheduleProjector? scheduleProjector = null)
        {
            var queries = new FacilityReportingPlanQueries(
                new EntityRepository<FacilityReportingPlan, TenantDbContext>(context),
                new EntityRepository<MeasureMapping, TenantDbContext>(context));

            var source = new DbBackedReportingPlanSource(
                NullLogger<DbBackedReportingPlanSource>.Instance,
                new EntityRepository<FacilityReportingPlan, TenantDbContext>(context),
                new EntityRepository<MeasureMapping, TenantDbContext>(context));

            return new FacilityReportingPlanLookAhead(queries, source,
                scheduleProjector
                    ?? new ReportingPlanScheduleProjector(NullLogger<ReportingPlanScheduleProjector>.Instance));
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

        /// <summary>
        /// An enrollment recorded before anyone mapped its measure: a measure name, no mapping.
        /// </summary>
        private static void AddUnmappedPlan(TenantDbContext context, string measure, ReportingPeriod period) =>
            context.FacilityReportingPlans.Add(new FacilityReportingPlan
            {
                FacilityId = FacilityId,
                MeasureMappingId = null,
                Measure = measure,
                ReportingMonth = period.Month,
                ReportingYear = period.Year,
                IsReporting = true
            });

        private static ReportingPeriodRange SixMonths() => ReportingPeriodRange.LookAhead(Anchor, 6);

        [Fact]
        public async Task UnmappedEnrollment_IsListedWithoutADqmRatherThanFailingTheRead()
        {
            using var context = CreateContext();
            AddUnmappedPlan(context, "NEWMEASURE", Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, SixMonths(), Anchor);

            // Storing an enrollment Link has no mapping for is the point of the schema change, so
            // reading one back has to work. Both reads stitch mappings on by dictionary lookup, and a
            // null key throws rather than missing -- this row used to make the endpoint 500.
            var recorded = page.Records.Single(period => !period.IsProjected);
            var measure = Assert.Single(recorded.Measures);

            Assert.Equal("NEWMEASURE", measure.Measure);
            Assert.Null(measure.MeasureMappingId);
            Assert.True(string.IsNullOrEmpty(measure.DQM));

            // No dQM means nothing for Link to run, so it schedules nothing -- but the enrollment is
            // still reported, which is what tells an admin there is a mapping to go and create.
            Assert.Empty(recorded.Schedule.Monthly);
            Assert.Empty(recorded.Schedule.Daily);
            Assert.Empty(recorded.Schedule.Weekly);
        }

        [Fact]
        public async Task UnmappedEnrollment_ProjectsForwardWithNoCadence()
        {
            using var context = CreateContext();
            AddUnmappedPlan(context, "NEWMEASURE", Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, SixMonths(), Anchor);

            // The projection reads the anchor period through DbBackedReportingPlanSource, the second
            // of the two dictionary lookups. Frequency comes off the mapping, so an enrollment with no
            // mapping has no cadence -- naming one would put a schedule nobody chose in front of the
            // facility for every month in the window.
            var projected = page.Records.First(period => period.IsProjected);
            var measure = Assert.Single(projected.Measures);

            Assert.Equal("NEWMEASURE", measure.Measure);
            Assert.Null(measure.Frequency);
            Assert.Empty(projected.Schedule.Monthly);
        }

        [Fact]
        public async Task RecordedMeasure_CarriesTheComponentItCameFrom()
        {
            using var context = CreateContext();
            var hob = AddMapping(context, "HOB");

            AddPlan(context, hob, Anchor);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context).GetAsync(FacilityId, SixMonths(), Anchor);

            // Per measure, not per period: one month can carry enrollments from both components, so a
            // single value on the period would have to pick one and misdescribe the others.
            var recorded = page.Records.Single(period => !period.IsProjected);
            Assert.Equal(ReportingComponents.Msc, Assert.Single(recorded.Measures).Component);
        }

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
        public async Task APeriodRecordedAsFullyWithdrawn_IsNotProjectedOver()
        {
            using var context = CreateContext();
            var hob = AddMapping(context, "HOB");

            AddPlan(context, hob, Anchor);
            AddPlan(context, hob, new ReportingPeriod(2026, 12), isReporting: false);
            await context.SaveChangesAsync();

            // isReporting: true is what the controller passes for an unfiltered request -- it defaults
            // the parameter rather than leaving it null -- so it is the path this has to be tested on.
            var page = await CreateLookAhead(context)
                .GetAsync(FacilityId, SixMonths(), Anchor, isReporting: true);

            // December has a plan on record saying the facility withdrew. Deciding the gaps from the
            // rows the isReporting filter left behind would make that indistinguishable from a month
            // nobody has spoken about, and the answer would be today's enrollment projected onto it --
            // telling the facility it reports HOB in a month DMRP has recorded that it does not.
            Assert.DoesNotContain(page.Records,
                period => period.ReportingYear == 2026 && period.ReportingMonth == 12);
        }

        [Fact]
        public async Task APartiallyWithdrawnPeriod_KeepsTheMeasuresItStillReports()
        {
            using var context = CreateContext();
            var hob = AddMapping(context, "HOB");
            var cauti = AddMapping(context, "CAUTI");
            var december = new ReportingPeriod(2026, 12);

            AddPlan(context, hob, Anchor);
            AddPlan(context, cauti, december);
            AddPlan(context, hob, december, isReporting: false);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context)
                .GetAsync(FacilityId, SixMonths(), Anchor, isReporting: true);

            // The counterpart: filtering still happens, it just no longer decides what counts as
            // recorded. December keeps CAUTI, drops the withdrawn HOB, and is not projected.
            var period = page.Records.Single(p => p.ReportingYear == 2026 && p.ReportingMonth == 12);
            Assert.False(period.IsProjected);
            Assert.Equal("CAUTI", Assert.Single(period.Measures).Measure);
        }

        [Fact]
        public async Task AskingForWithdrawalsOnly_StillSeesAFullyWithdrawnPeriod()
        {
            using var context = CreateContext();
            var hob = AddMapping(context, "HOB");

            AddPlan(context, hob, Anchor);
            AddPlan(context, hob, new ReportingPeriod(2026, 12), isReporting: false);
            await context.SaveChangesAsync();

            var page = await CreateLookAhead(context)
                .GetAsync(FacilityId, SixMonths(), Anchor, isReporting: false);

            // Moving the filter out of the query must not lose the rows it selects for.
            var period = Assert.Single(page.Records);
            Assert.Equal(12, period.ReportingMonth);
            Assert.Equal("HOB", Assert.Single(period.Measures).Measure);
        }

        [Fact]
        public async Task ProjectingOverManyMonths_DoesNotWarnPerMonth()
        {
            using var context = CreateContext();
            var logger = new Mock<ILogger<ReportingPlanScheduleProjector>>();

            // A measure whose mapping carries no dQM is what the warning is about.
            AddPlan(context, AddMapping(context, "HOB", dqm: " "), Anchor);
            await context.SaveChangesAsync();

            var lookAhead = CreateLookAhead(context,
                new ReportingPlanScheduleProjector(logger.Object));

            await lookAhead.GetAsync(FacilityId, ReportingPeriodRange.LookAhead(Anchor, 24), Anchor);

            // The read projects the same enrollment over every month in the window, so warning inside
            // the projection would emit the identical line up to 24 times for one GET. The write path
            // that saves the enrollment is where the mapping can be fixed, and it still warns.
            logger.Verify(
                item => item.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
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
