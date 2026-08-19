using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    /// <summary>
    /// With DMRP enabled, a facility's scheduled reports stop being something a caller supplies and
    /// become something derived from its reporting plans. These cover that substitution and the
    /// clean-up that follows a facility being removed.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class DmrpFacilityOperationsTests
    {
        private const string FacilityId = "100";

        /// <summary>
        /// Fixed so the period the source is asked for is predictable. June in UTC is still May in
        /// Chicago at this instant, which is what the timezone test turns on.
        /// </summary>
        private static readonly DateTimeOffset FixedNow = new(2026, 6, 1, 2, 30, 0, TimeSpan.Zero);

        private readonly Mock<IFacilityOperations> _inner = new();
        private readonly Mock<IReportingPlanSource> _plans = new();
        private readonly Mock<IFacilityReportingPlanManager> _planManager = new();
        private readonly Mock<IEntityRepository<FacilityReportingPlan>> _planRepository = new();

        private DmrpFacilityOperations CreateOperations() =>
            new(NullLogger<DmrpFacilityOperations>.Instance, _inner.Object, _plans.Object, _planManager.Object,
                _planRepository.Object, new FixedTimeProvider(FixedNow));

        private void GivenPlan(params ReportingPlanEntry[] entries) =>
            _plans.Setup(p => p.GetForPeriodAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries);

        private static FacilityModel Facility(TenantScheduledReportConfig? scheduledReports = null,
            string timeZone = "UTC") => new()
            {
                FacilityId = FacilityId,
                FacilityName = "Test Facility",
                TimeZone = timeZone,
                ScheduledReports = scheduledReports ?? new TenantScheduledReportConfig()
            };

        private static TenantScheduledReportConfig Schedule(string[]? daily = null, string[]? weekly = null,
            string[]? monthly = null) => new()
            {
                Daily = daily ?? Array.Empty<string>(),
                Weekly = weekly ?? Array.Empty<string>(),
                Monthly = monthly ?? Array.Empty<string>()
            };

        [Fact]
        public async Task Create_derives_the_schedule_from_the_facilitys_reporting_plans()
        {
            GivenPlan(
                new ReportingPlanEntry("HOB", "dqm-monthly", Frequency.Monthly),
                new ReportingPlanEntry("HTCDI", "dqm-daily", Frequency.Daily));

            var facility = Facility();

            await CreateOperations().CreateAsync(facility);

            _inner.Verify(i => i.CreateAsync(facility, It.IsAny<CancellationToken>()), Times.Once);

            Assert.Equal(new[] { "dqm-daily" }, facility.ScheduledReports.Daily);
            Assert.Equal(new[] { "dqm-monthly" }, facility.ScheduledReports.Monthly);
            Assert.Empty(facility.ScheduledReports.Weekly);
        }

        [Fact]
        public async Task Update_derives_the_schedule_from_the_facilitys_reporting_plans()
        {
            GivenPlan(new ReportingPlanEntry("HOB", "dqm-monthly", Frequency.Monthly));

            var existing = Facility(Schedule(monthly: new[] { "dqm-was-here" }));
            var updated = Facility();

            await CreateOperations().UpdateAsync(existing, updated);

            _inner.Verify(i => i.UpdateAsync(existing, updated, It.IsAny<CancellationToken>()), Times.Once);

            Assert.Equal(new[] { "dqm-monthly" }, updated.ScheduledReports.Monthly);
        }

        /// <summary>
        /// Two NHSN measures can map to the same dQM, and the host refuses a schedule that names one
        /// twice.
        /// </summary>
        [Fact]
        public async Task Names_a_dqm_once_when_two_measures_map_to_it()
        {
            GivenPlan(
                new ReportingPlanEntry("HOB", "ach-monthly", Frequency.Monthly),
                new ReportingPlanEntry("HTCDI", "ach-monthly", Frequency.Monthly));

            var facility = Facility();

            await CreateOperations().CreateAsync(facility);

            Assert.Equal(new[] { "ach-monthly" }, facility.ScheduledReports.Monthly);
        }

        [Fact]
        public async Task Excludes_an_enrolled_measure_that_has_no_dqm_mapped()
        {
            GivenPlan(
                new ReportingPlanEntry("HOB", "dqm-monthly", Frequency.Monthly),
                new ReportingPlanEntry("NEWMEASURE", string.Empty, Frequency.Adhoc));

            var facility = Facility();

            await CreateOperations().CreateAsync(facility);

            Assert.Equal(new[] { "dqm-monthly" }, facility.ScheduledReports.Monthly);
            Assert.Empty(facility.ScheduledReports.Daily);
            Assert.Empty(facility.ScheduledReports.Weekly);
        }

        /// <summary>
        /// A facility enrolled in nothing is scheduled for nothing. The three arrays must still be
        /// present: the host reads them without a null check.
        /// </summary>
        [Fact]
        public async Task Schedules_no_reports_for_a_facility_with_no_reporting_plans()
        {
            GivenPlan();

            var facility = Facility();

            await CreateOperations().CreateAsync(facility);

            Assert.Empty(facility.ScheduledReports.Daily);
            Assert.Empty(facility.ScheduledReports.Weekly);
            Assert.Empty(facility.ScheduledReports.Monthly);
        }

        [Fact]
        public async Task Reads_the_reporting_period_in_the_facilitys_own_timezone()
        {
            GivenPlan();

            // FixedNow is 1 June 02:30 UTC, which is 31 May in Chicago.
            await CreateOperations().CreateAsync(Facility(timeZone: "America/Chicago"));

            _plans.Verify(p => p.GetForPeriodAsync(FacilityId, 5, 2026, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// The host validates the timezone and answers with a message naming it, so an unusable one has
        /// to reach the host rather than failing here first.
        /// </summary>
        [Fact]
        public async Task Falls_back_to_utc_when_the_facilitys_timezone_is_unusable()
        {
            GivenPlan();

            await CreateOperations().CreateAsync(Facility(timeZone: "Not/AZone"));

            _plans.Verify(p => p.GetForPeriodAsync(FacilityId, 6, 2026, It.IsAny<CancellationToken>()), Times.Once);
            _inner.Verify(i => i.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("daily")]
        [InlineData("weekly")]
        [InlineData("monthly")]
        public async Task Create_refuses_a_caller_supplied_schedule(string frequency)
        {
            var schedule = frequency switch
            {
                "daily" => Schedule(daily: new[] { "dqm" }),
                "weekly" => Schedule(weekly: new[] { "dqm" }),
                _ => Schedule(monthly: new[] { "dqm" })
            };

            var operations = CreateOperations();

            await Assert.ThrowsAsync<ScheduledReportsNotAcceptedException>(
                () => operations.CreateAsync(Facility(schedule)));

            _inner.Verify(i => i.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// An empty block is the only way a caller can decline to set a schedule: the three arrays are
        /// non-nullable, so model binding rejects a request that leaves scheduledReports out entirely.
        /// It must therefore pass rather than be treated as a caller-supplied schedule.
        /// </summary>
        [Fact]
        public async Task Accepts_an_explicitly_empty_schedule()
        {
            GivenPlan(new ReportingPlanEntry("HOB", "dqm-monthly", Frequency.Monthly));

            var facility = Facility(Schedule());

            await CreateOperations().CreateAsync(facility);

            _inner.Verify(i => i.CreateAsync(facility, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { "dqm-monthly" }, facility.ScheduledReports.Monthly);
        }

        /// <summary>
        /// The message has to name a remedy the caller can carry out, which "resubmit without
        /// scheduledReports" is not.
        /// </summary>
        [Fact]
        public async Task Refusal_tells_the_caller_to_send_empty_arrays()
        {
            var operations = CreateOperations();

            var ex = await Assert.ThrowsAsync<ScheduledReportsNotAcceptedException>(
                () => operations.CreateAsync(Facility(Schedule(monthly: new[] { "dqm" }))));

            Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("without scheduledReports", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Update_refuses_a_caller_supplied_schedule()
        {
            var operations = CreateOperations();

            await Assert.ThrowsAsync<ScheduledReportsNotAcceptedException>(
                () => operations.UpdateAsync(Facility(), Facility(Schedule(monthly: new[] { "dqm" }))));

            _inner.Verify(
                i => i.UpdateAsync(It.IsAny<FacilityModel>(), It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// The schedule already on the facility being replaced is not the caller's input, so it must
        /// not be treated as one.
        /// </summary>
        [Fact]
        public async Task Update_ignores_the_schedule_on_the_facility_being_replaced()
        {
            GivenPlan(new ReportingPlanEntry("HOB", "dqm-monthly", Frequency.Monthly));

            await CreateOperations().UpdateAsync(Facility(Schedule(monthly: new[] { "dqm-was-here" })), Facility());

            _inner.Verify(
                i => i.UpdateAsync(It.IsAny<FacilityModel>(), It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Delete_removes_the_facility_before_its_reporting_plans()
        {
            var sequence = new List<string>();

            _planRepository.Setup(r => r.StartTransactionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("begin"))
                .Returns(Task.CompletedTask);

            _inner.Setup(i => i.DeleteAsync(FacilityId, It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("facility"))
                .Returns(Task.CompletedTask);

            _planManager.Setup(m => m.DeleteForFacilityAsync(FacilityId, It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("plans"))
                .ReturnsAsync(2);

            _planRepository.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => sequence.Add("commit"))
                .Returns(Task.CompletedTask);

            await CreateOperations().DeleteAsync(FacilityId);

            Assert.Equal(new[] { "begin", "facility", "plans", "commit" }, sequence);
        }

        /// <summary>
        /// Both deletes are one unit. Without the rollback, a failure here left the facility gone and
        /// its plans behind - nothing collected them, they blocked measure mapping deletes, and a
        /// facility later created with the same id inherited them.
        /// </summary>
        [Fact]
        public async Task Delete_rolls_back_the_facility_when_the_plan_cleanup_fails()
        {
            _planManager.Setup(m => m.DeleteForFacilityAsync(FacilityId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("plan cleanup failed"));

            var operations = CreateOperations();

            await Assert.ThrowsAsync<InvalidOperationException>(() => operations.DeleteAsync(FacilityId));

            _planRepository.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _planRepository.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// A rollback that fails must not replace the error that caused it, or the caller is told about
        /// the cleanup rather than the thing that actually went wrong.
        /// </summary>
        [Fact]
        public async Task Delete_surfaces_the_original_failure_even_when_the_rollback_fails()
        {
            _inner.Setup(i => i.DeleteAsync(FacilityId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ApplicationException("facility delete refused"));

            _planRepository.Setup(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("no transaction"));

            var operations = CreateOperations();

            var thrown = await Assert.ThrowsAsync<ApplicationException>(() => operations.DeleteAsync(FacilityId));

            Assert.Equal("facility delete refused", thrown.Message);
        }

        /// <summary>
        /// Plans deleted ahead of a delete the host refuses would leave a facility that reports nothing.
        /// </summary>
        [Fact]
        public async Task Delete_keeps_the_reporting_plans_when_the_facility_delete_fails()
        {
            _inner.Setup(i => i.DeleteAsync(FacilityId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ApplicationException("nope"));

            var operations = CreateOperations();

            await Assert.ThrowsAsync<ApplicationException>(() => operations.DeleteAsync(FacilityId));

            _planManager.Verify(m => m.DeleteForFacilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// A soft-deleted facility can be restored, and its plans are the record of what it was enrolled
        /// to report while it was active.
        /// </summary>
        [Fact]
        public async Task Soft_delete_keeps_the_reporting_plans()
        {
            await CreateOperations().SoftDeleteAsync(FacilityId);

            _inner.Verify(i => i.SoftDeleteAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
            _planManager.Verify(m => m.DeleteForFacilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Restore_only_restores_the_facility()
        {
            var facility = Facility();

            await CreateOperations().RestoreAsync(facility);

            _inner.Verify(i => i.RestoreAsync(facility, It.IsAny<CancellationToken>()), Times.Once);
            _plans.VerifyNoOtherCalls();
            _planManager.VerifyNoOtherCalls();
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _utcNow;

            public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

            public override DateTimeOffset GetUtcNow() => _utcNow;
        }
    }
}
