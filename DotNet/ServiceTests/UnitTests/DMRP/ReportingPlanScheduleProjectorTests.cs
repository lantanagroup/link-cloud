using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests.DMRP
{
    /// <summary>
    /// Covers who hears about an enrollment the projector has to drop.
    /// </summary>
    /// <remarks>
    /// A measure with no dQM mapped is recorded with a null dQM precisely so it surfaces rather than
    /// being lost, and the projector is where it surfaces. The projector is shared between the write
    /// that saves a facility's schedule and the read that projects a look-ahead, though, and those two
    /// want opposite things from it: the write happens once and is where the mapping can be fixed, the
    /// read projects the same enrollment over every month in the window and would repeat one identical
    /// line up to twenty-four times for a single GET.
    /// </remarks>
    [Trait("Category", "UnitTests")]
    public class ReportingPlanScheduleProjectorTests
    {
        private static readonly ReportingPeriod Period = new(2026, 10);

        private static readonly List<ReportingPlanEntry> Unmapped =
            [new ReportingPlanEntry("HOB", string.Empty, Frequency.Monthly)];

        [Fact]
        public void ByDefault_AnUnmappedMeasureIsWarnedAbout()
        {
            var logger = new Mock<ILogger<ReportingPlanScheduleProjector>>();

            new ReportingPlanScheduleProjector(logger.Object).Project(Unmapped, "facility-1", Period);

            // The default has to stay the warning one: a caller that has not thought about it is the
            // write path, and silence there loses the only signal that a mapping is missing.
            VerifyWarnings(logger, Times.Once());
        }

        [Fact]
        public void WarningsOff_TheMeasureIsStillDroppedSilently()
        {
            var logger = new Mock<ILogger<ReportingPlanScheduleProjector>>();

            var schedule = new ReportingPlanScheduleProjector(logger.Object)
                .Project(Unmapped, "facility-1", Period, warnOnUnmapped: false);

            // Only the logging is suppressed. The measure is excluded either way, because a schedule
            // naming a dQM that does not exist would promise a report Link cannot run.
            Assert.Empty(schedule.Monthly);
            VerifyWarnings(logger, Times.Never());
        }

        [Fact]
        public void AMappedMeasureNeverWarns()
        {
            var logger = new Mock<ILogger<ReportingPlanScheduleProjector>>();

            var schedule = new ReportingPlanScheduleProjector(logger.Object).Project(
                [new ReportingPlanEntry("HOB", "dqm-hob", Frequency.Monthly)], "facility-1", Period);

            Assert.Equal("dqm-hob", Assert.Single(schedule.Monthly));
            VerifyWarnings(logger, Times.Never());
        }

        private static void VerifyWarnings(Mock<ILogger<ReportingPlanScheduleProjector>> logger, Times times) =>
            logger.Verify(
                item => item.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times);
    }
}
