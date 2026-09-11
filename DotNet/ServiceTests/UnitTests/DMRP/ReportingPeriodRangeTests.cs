using LantanaGroup.Link.DMRP.Models;

namespace UnitTests.DMRP
{
    /// <summary>
    /// The look-ahead window is the one piece of date arithmetic in the reporting plan reads, and the
    /// place a six-month range quietly becomes five or seven. These pin the boundaries.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class ReportingPeriodRangeTests
    {
        [Fact]
        public void LookAhead_CountsTheAnchorAsTheFirstMonth()
        {
            var range = ReportingPeriodRange.LookAhead(new ReportingPeriod(2026, 5), monthsAhead: 6);

            Assert.Equal(new ReportingPeriod(2026, 5), range.From);
            Assert.Equal(new ReportingPeriod(2026, 10), range.To);
        }

        [Fact]
        public void LookAhead_OfOneMonth_IsTheAnchorAlone()
        {
            var range = ReportingPeriodRange.LookAhead(new ReportingPeriod(2026, 5), monthsAhead: 1);

            Assert.Equal(range.From, range.To);
        }

        [Fact]
        public void LookAhead_RollsOverTheYear()
        {
            var range = ReportingPeriodRange.LookAhead(new ReportingPeriod(2026, 10), monthsAhead: 6);

            Assert.Equal(new ReportingPeriod(2026, 10), range.From);
            Assert.Equal(new ReportingPeriod(2027, 3), range.To);
        }

        [Fact]
        public void LookAhead_FromDecember_LandsInTheFollowingYear()
        {
            var range = ReportingPeriodRange.LookAhead(new ReportingPeriod(2026, 12), monthsAhead: 2);

            Assert.Equal(new ReportingPeriod(2027, 1), range.To);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void LookAhead_RefusesAWindowOfNoPeriods(int monthsAhead)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReportingPeriodRange.LookAhead(new ReportingPeriod(2026, 5), monthsAhead));
        }

        [Fact]
        public void AddMonths_RefusesAMonthThatCannotExist()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReportingPeriod(2026, 13).AddMonths(1));
        }
    }
}
