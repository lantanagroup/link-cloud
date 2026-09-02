using System.Linq.Expressions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Data.Entities;
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
    /// The sync is a reconciliation, so these run it against a real context and assert on the rows
    /// afterwards. Only the API is stubbed - what it returns is the whole input.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class DmrpReportingPlanSyncTests : IDisposable
    {
        private const string FacilityId = "100";
        private const int Month = 10;
        private const int Year = 2026;

        private readonly SqliteConnection _connection;

        public DmrpReportingPlanSyncTests()
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

        /// <summary>Returns whatever the test says DMRP said.</summary>
        private sealed class StubClient : IDmrpApiClient
        {
            private readonly IReadOnlyList<DmrpReportingPlanEntry> _entries;

            public StubClient(params DmrpReportingPlanEntry[] entries) => _entries = entries;

            public Task<IReadOnlyList<DmrpReportingPlanEntry>> GetReportingPlanAsync(string facilityId, int month,
                int year, CancellationToken cancellationToken = default) => Task.FromResult(_entries);
        }

        /// <summary>
        /// Delegates to a real repository, running one action immediately before the first save.
        /// </summary>
        /// <remarks>
        /// The seam a concurrency test needs and the sync does not otherwise offer. Everything the
        /// sync does happens either side of the save, so this is the only point at which a
        /// competing writer can be made to commit after this one has already read. Members the sync
        /// does not use throw rather than delegate: a double that quietly answers calls it was
        /// never meant to serve hides the test drifting away from the code.
        /// </remarks>
        private sealed class SaveInterceptingRepository : IEntityRepository<FacilityReportingPlan>
        {
            private readonly IEntityRepository<FacilityReportingPlan> _inner;
            private readonly Func<Task> _beforeFirstSave;
            private bool _fired;

            public SaveInterceptingRepository(IEntityRepository<FacilityReportingPlan> inner,
                Func<Task> beforeFirstSave)
            {
                _inner = inner;
                _beforeFirstSave = beforeFirstSave;
            }

            public async Task SaveChangesAsync(CancellationToken cancellationToken)
            {
                if (!_fired)
                {
                    _fired = true;
                    await _beforeFirstSave();
                }

                await _inner.SaveChangesAsync(cancellationToken);
            }

            public Task<List<FacilityReportingPlan>> FindAsync(
                Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken cancellationToken) =>
                _inner.FindAsync(predicate, cancellationToken);

            public Task<FacilityReportingPlan> AddAsync(FacilityReportingPlan entity,
                CancellationToken cancellationToken) =>
                _inner.AddAsync(entity, cancellationToken);

            public void Remove(FacilityReportingPlan entity) => _inner.Remove(entity);

            public void Update(FacilityReportingPlan entity) => _inner.Update(entity);

            public Task SaveChangesAsync() => throw new NotSupportedException();
            public Task<FacilityReportingPlan> AddAsync(FacilityReportingPlan entity) => throw new NotSupportedException();
            public Task AddRangeAsync(IEnumerable<FacilityReportingPlan> entity) => throw new NotSupportedException();
            public Task<FacilityReportingPlan> GetAsync(object id) => throw new NotSupportedException();
            public Task<FacilityReportingPlan> GetAsync(object id, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<FacilityReportingPlan>> GetAllAsync() => throw new NotSupportedException();
            public Task<List<FacilityReportingPlan>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<List<FacilityReportingPlan>> FindAsync(Expression<Func<FacilityReportingPlan, bool>> predicate) => throw new NotSupportedException();
            public Task<FacilityReportingPlan?> FirstOrDefaultAsync(Expression<Func<FacilityReportingPlan, bool>> predicate) => throw new NotSupportedException();
            public Task<FacilityReportingPlan?> FirstOrDefaultAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<FacilityReportingPlan> FirstAsync(Expression<Func<FacilityReportingPlan, bool>> predicate) => throw new NotSupportedException();
            public Task<FacilityReportingPlan> FirstAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<FacilityReportingPlan?> SingleOrDefaultAsync(Expression<Func<FacilityReportingPlan, bool>> predicate) => throw new NotSupportedException();
            public Task<FacilityReportingPlan?> SingleOrDefaultAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<FacilityReportingPlan> SingleAsync(Expression<Func<FacilityReportingPlan, bool>> predicate) => throw new NotSupportedException();
            public Task<FacilityReportingPlan> SingleAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<int> ExecuteDeleteAsync(Expression<Func<FacilityReportingPlan, bool>> predicate) => throw new NotSupportedException();
            public Task<int> ExecuteDeleteAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<(List<FacilityReportingPlan>, PaginationMetadata)> SearchAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber) => throw new NotSupportedException();
            public Task<(List<FacilityReportingPlan>, PaginationMetadata)> SearchAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<HealthCheckResult> HealthCheck(int eventId) => throw new NotSupportedException();
            public Task<HealthCheckResult> HealthCheck(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task StartTransactionAsync() => throw new NotSupportedException();
            public Task StartTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task CommitTransactionAsync() => throw new NotSupportedException();
            public Task CommitTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task RollbackTransactionAsync() => throw new NotSupportedException();
            public Task RollbackTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<bool> AnyAsync(Expression<Func<FacilityReportingPlan, bool>> predicate) => throw new NotSupportedException();
            public Task<bool> AnyAsync(Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private static DmrpReportingPlanSync CreateSync(TenantDbContext context, params DmrpReportingPlanEntry[] entries) =>
            new(new StubClient(entries),
                new EntityRepository<FacilityReportingPlan, TenantDbContext>(context),
                new EntityRepository<MeasureMapping, TenantDbContext>(context),
                NullLogger<DmrpReportingPlanSync>.Instance);

        private static DmrpReportingPlanEntry Entry(string measure, string component = ReportingComponents.Msc) =>
            new(component, measure, Month, Year);

        /// <summary>
        /// An entry answering for a period other than the one asked for. The client keeps an entry's
        /// own month and year in preference to the requested ones, so this is a shape the sync has to
        /// handle rather than a hypothetical.
        /// </summary>
        private static DmrpReportingPlanEntry OffPeriodEntry(string measure, int month, int year,
            string component = ReportingComponents.Msc) =>
            new(component, measure, month, year);

        [Fact]
        public async Task SyncAsync_LosingTheRaceToAnotherSync_ReconcilesInsteadOfFailing()
        {
            using var setup = CreateContext();
            AddMapping(setup, "HOB");
            await setup.SaveChangesAsync();

            using var winnerContext = CreateContext();
            using var loserContext = CreateContext();

            // The overlap has to be built deliberately: the loser must read before the winner
            // commits and save after it, which is the only ordering that produces the conflict.
            // Running one sync and then the other proves nothing, because the second one's read
            // simply finds the first one's row.
            var winnerCommitted = false;

            var plans = new SaveInterceptingRepository(
                new EntityRepository<FacilityReportingPlan, TenantDbContext>(loserContext),
                async () =>
                {
                    if (winnerCommitted)
                    {
                        return;
                    }

                    winnerCommitted = true;
                    await CreateSync(winnerContext, Entry("HOB")).SyncAsync(FacilityId, Month, Year);
                });

            var loser = new DmrpReportingPlanSync(
                new StubClient([Entry("HOB")]),
                plans,
                new EntityRepository<MeasureMapping, TenantDbContext>(loserContext),
                NullLogger<DmrpReportingPlanSync>.Instance);

            var result = await loser.SyncAsync(FacilityId, Month, Year);

            // The refresh answers rather than throwing. A DbUpdateException would reach the
            // controller, which catches DmrpApiException and not this, so two admins refreshing the
            // same facility at once would see a 500.
            var rows = await setup.FacilityReportingPlans
                .Where(p => p.FacilityId == FacilityId)
                .ToListAsync();

            Assert.Single(rows);

            // On the second attempt the winner's row is there to be found, so there is nothing left
            // to insert and the sync reports no work rather than claiming a row it did not write.
            Assert.Equal(0, result.Recorded);
        }

        [Fact]
        public async Task SyncAsync_AnEntryForAnotherPeriod_IsRecordedOnceAcrossRepeatedRuns()
        {
            using var context = CreateContext();
            AddMapping(context, "HOB");
            await context.SaveChangesAsync();

            var sync = CreateSync(context, Entry("HOB"), OffPeriodEntry("HOB", Month + 1, Year));

            await sync.SyncAsync(FacilityId, Month, Year);
            await sync.SyncAsync(FacilityId, Month, Year);

            // The comparison set used to be loaded for the requested period alone, so the off-period
            // row could never match itself and was inserted again on every refresh -- until the second
            // one hit the unique index, and because the whole sync saves at once, took every
            // legitimate change of that run down with it.
            var rows = await context.FacilityReportingPlans
                .Where(p => p.FacilityId == FacilityId && p.ReportingMonth == Month + 1)
                .ToListAsync();

            Assert.Single(rows);
        }

        [Fact]
        public async Task SyncAsync_TheSameEnrollmentListedTwice_IsRecordedOnce()
        {
            using var context = CreateContext();
            AddMapping(context, "HOB");
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB"), Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // Two adds and one SaveChanges is a unique-index violation, and the exception is not a
            // DmrpApiException, so it would surface as a 500 rather than the documented 502.
            var rows = await context.FacilityReportingPlans
                .Where(p => p.FacilityId == FacilityId)
                .ToListAsync();

            Assert.Single(rows);
        }

        [Fact]
        public async Task SyncAsync_DoesNotWithdrawARowInAPeriodItDidNotAskAbout()
        {
            using var context = CreateContext();
            AddMapping(context, "HOB");
            var other = AddPlan(context, "HOB");
            other.ReportingMonth = Month + 1;
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // Absence is how DMRP says "withdrawn", but only for the period the question named. The
            // widened load must not turn another period's rows into withdrawals just by making them
            // visible to the comparison.
            var untouched = await context.FacilityReportingPlans
                .SingleAsync(p => p.FacilityId == FacilityId && p.ReportingMonth == Month + 1);

            Assert.True(untouched.IsReporting);
        }

        private static MeasureMapping AddMapping(TenantDbContext context, string measure) =>
            context.MeasureMappings.Add(new MeasureMapping
            {
                Measure = measure,
                DQM = $"dqm-{measure}",
                Frequency = Frequency.Monthly
            }).Entity;

        private static FacilityReportingPlan AddPlan(TenantDbContext context, string measure,
            string component = ReportingComponents.Msc, bool isReporting = true, string? mappingId = null) =>
            context.FacilityReportingPlans.Add(new FacilityReportingPlan
            {
                FacilityId = FacilityId,
                Component = component,
                Measure = measure,
                MeasureMappingId = mappingId,
                ReportingMonth = Month,
                ReportingYear = Year,
                IsReporting = isReporting
            }).Entity;

        [Fact]
        public async Task Sync_RecordsAnEnrollmentLinkHadNotSeen()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context, "HOB");
            await context.SaveChangesAsync();

            var result = await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            Assert.Equal(1, result.Recorded);

            var plan = await context.FacilityReportingPlans.SingleAsync();
            Assert.Equal("HOB", plan.Measure);
            Assert.Equal(mapping.Id, plan.MeasureMappingId);
            Assert.Equal(ReportingComponents.Msc, plan.Component);
            Assert.True(plan.IsReporting);
        }

        [Fact]
        public async Task Sync_RecordsAMeasureWithNoMappingAndLeavesItUnmapped()
        {
            using var context = CreateContext();

            var result = await CreateSync(context, Entry("UNMAPPED")).SyncAsync(FacilityId, Month, Year);

            // The enrollment is a fact whether or not Link can evaluate it. Dropping it would hide
            // from the admin that there is something to map.
            Assert.Equal(1, result.Recorded);
            Assert.Equal(1, result.Unmapped);

            var plan = await context.FacilityReportingPlans.SingleAsync();
            Assert.Equal("UNMAPPED", plan.Measure);
            Assert.Null(plan.MeasureMappingId);
        }

        [Fact]
        public async Task Sync_TagsEachEnrollmentWithTheComponentItCameFrom()
        {
            using var context = CreateContext();

            await CreateSync(context, Entry("HOB"), Entry("HAI", ReportingComponents.Ps))
                .SyncAsync(FacilityId, Month, Year);

            var plans = await context.FacilityReportingPlans.OrderBy(p => p.Measure).ToListAsync();

            Assert.Equal(ReportingComponents.Ps, plans[0].Component);
            Assert.Equal(ReportingComponents.Msc, plans[1].Component);
        }

        [Fact]
        public async Task Sync_MarksAnEnrollmentDmrpNoLongerReturnsAsNotReporting()
        {
            using var context = CreateContext();
            AddPlan(context, "HOB");
            AddPlan(context, "CAUTI");
            await context.SaveChangesAsync();

            var result = await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // DMRP conveys a withdrawal only by no longer returning the measure, so the diff is the
            // only thing that can detect one.
            Assert.Equal(1, result.Withdrawn);

            var withdrawn = await context.FacilityReportingPlans.SingleAsync(p => p.Measure == "CAUTI");
            Assert.False(withdrawn.IsReporting);
        }

        [Fact]
        public async Task Sync_KeepsAWithdrawnRowRatherThanDeletingIt()
        {
            using var context = CreateContext();
            AddPlan(context, "CAUTI");
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // The row is the record of what DMRP said and when it changed; the facility's own view
            // shows it as history.
            Assert.Equal(2, await context.FacilityReportingPlans.CountAsync());
        }

        [Fact]
        public async Task Sync_ReinstatesAnEnrollmentDmrpStartedReturningAgain()
        {
            using var context = CreateContext();
            AddPlan(context, "HOB", isReporting: false);
            await context.SaveChangesAsync();

            var result = await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            Assert.Equal(1, result.Reinstated);
            Assert.Equal(0, result.Recorded);
            Assert.True((await context.FacilityReportingPlans.SingleAsync()).IsReporting);
        }

        [Fact]
        public async Task Sync_FillsInAMappingThatHasSinceBeenCreated()
        {
            using var context = CreateContext();
            AddPlan(context, "HOB");
            var mapping = AddMapping(context, "HOB");
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            Assert.Equal(mapping.Id, (await context.FacilityReportingPlans.SingleAsync()).MeasureMappingId);
        }

        [Fact]
        public async Task Sync_DoesNotClearAMappingItCouldNotResolve()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context, "SOMETHING-ELSE");
            AddPlan(context, "HOB", mappingId: mapping.Id);
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // Clearing it would undo an admin's work on the strength of a lookup that missed.
            Assert.Equal(mapping.Id, (await context.FacilityReportingPlans.SingleAsync()).MeasureMappingId);
        }

        [Fact]
        public async Task Sync_ResolvesTheMappingWhateverTheCasing()
        {
            using var context = CreateContext();
            var mapping = AddMapping(context, "hob");
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            Assert.Equal(mapping.Id, (await context.FacilityReportingPlans.SingleAsync()).MeasureMappingId);
        }

        [Fact]
        public async Task Sync_AnEmptyPlanChangesNothing()
        {
            using var context = CreateContext();
            AddPlan(context, "HOB");
            await context.SaveChangesAsync();

            var result = await CreateSync(context).SyncAsync(FacilityId, Month, Year);

            // An empty plan and a facility DMRP has nothing to say about are indistinguishable, so
            // an empty answer is treated as no information rather than as a full withdrawal.
            Assert.Equal(DmrpSyncResult.Nothing, result);
            Assert.True((await context.FacilityReportingPlans.SingleAsync()).IsReporting);
        }

        [Fact]
        public async Task Sync_DoesNotWithdrawAComponentThatSaidNothing()
        {
            using var context = CreateContext();
            AddPlan(context, "HAI", ReportingComponents.Ps);
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // Only the medicine plan answered. Withdrawing patient safety on that basis would let
            // one component's silence retire the other's enrollments.
            Assert.True((await context.FacilityReportingPlans.SingleAsync(p => p.Measure == "HAI")).IsReporting);
        }

        [Fact]
        public async Task Sync_LeavesOtherPeriodsAlone()
        {
            using var context = CreateContext();

            context.FacilityReportingPlans.Add(new FacilityReportingPlan
            {
                FacilityId = FacilityId,
                Component = ReportingComponents.Msc,
                Measure = "CAUTI",
                ReportingMonth = 9,
                ReportingYear = Year,
                IsReporting = true
            });

            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // A plan for October says nothing about September.
            Assert.True((await context.FacilityReportingPlans.SingleAsync(p => p.ReportingMonth == 9)).IsReporting);
        }

        [Fact]
        public async Task Sync_LeavesOtherFacilitiesAlone()
        {
            using var context = CreateContext();

            context.FacilityReportingPlans.Add(new FacilityReportingPlan
            {
                FacilityId = "200",
                Component = ReportingComponents.Msc,
                Measure = "CAUTI",
                ReportingMonth = Month,
                ReportingYear = Year,
                IsReporting = true
            });

            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            Assert.True((await context.FacilityReportingPlans.SingleAsync(p => p.FacilityId == "200")).IsReporting);
        }

        [Fact]
        public async Task Sync_RunTwice_ChangesNothingTheSecondTime()
        {
            using var context = CreateContext();
            AddMapping(context, "HOB");
            await context.SaveChangesAsync();

            await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);
            var second = await CreateSync(context, Entry("HOB")).SyncAsync(FacilityId, Month, Year);

            // Idempotent: whatever eventually calls this may well call it more than once.
            Assert.Equal(DmrpSyncResult.Nothing, second);
            Assert.Equal(1, await context.FacilityReportingPlans.CountAsync());
        }
    }
}
