using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    /// <summary>
    /// The one place a facility's current reporting period is worked out, for the write path and the
    /// look-ahead reads alike. Facilities are only ever in US states and territories, so the cases are
    /// chosen from those zones, in both directions from UTC - UTC+12 to UTC-11.
    /// </summary>
    /// <remarks>
    /// Assertions are on the resolved period only. .NET reports <c>SupportsDaylightSavingTime</c>
    /// differently on Windows and Linux for the territory zones, so nothing here touches TimeZoneInfo.
    /// </remarks>
    [Trait("Category", "UnitTests")]
    public class FacilityReportingPeriodResolverTests
    {
        private const string FacilityId = "100";

        private readonly Mock<ILogger<FacilityReportingPeriodResolver>> _logger = new();
        private readonly Mock<IFacilityTimeZoneSource> _timeZoneSource = new();

        private FacilityReportingPeriodResolver CreateResolver(TimeProvider clock) =>
            new(_logger.Object, clock, _timeZoneSource.Object);

        private FacilityReportingPeriodResolver CreateResolver(string utcNow) =>
            CreateResolver(new FakeTimeProvider(DateTimeOffset.Parse(utcNow)));

        private void GivenStoredTimeZone(string? timeZone) =>
            _timeZoneSource.Setup(s => s.GetTimeZoneAsync(FacilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(timeZone);

        private void VerifyWarnings(Times times) =>
            _logger.Verify(
                item => item.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times);

        // ---------------------------------------------------------------------------------
        // Resolve - the timezone is supplied
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// The ticket's case, plus the edges in both directions. Every instant was checked against
        /// TimeZoneInfo in the deployed Tenant image.
        /// </summary>
        [Theory]
        // Behind UTC: the UTC month has turned over, the facility's has not.
        [InlineData("2026-11-01T02:00:00Z", "America/Los_Angeles", 2026, 10)]
        [InlineData("2026-11-01T02:00:00Z", "UTC", 2026, 11)]
        [InlineData("2026-10-01T03:59:00Z", "America/New_York", 2026, 9)]   // EDT, UTC-4
        [InlineData("2026-10-01T04:00:00Z", "America/New_York", 2026, 10)]
        [InlineData("2027-01-01T04:59:00Z", "America/New_York", 2026, 12)]  // EST, UTC-5
        [InlineData("2027-01-01T05:00:00Z", "America/New_York", 2027, 1)]
        [InlineData("2026-10-01T10:59:00Z", "Pacific/Pago_Pago", 2026, 9)]  // UTC-11, the widest behind
        [InlineData("2026-10-01T11:00:00Z", "Pacific/Pago_Pago", 2026, 10)]
        [InlineData("2027-01-01T05:00:00Z", "Pacific/Pago_Pago", 2026, 12)] // year rollover
        [InlineData("2026-10-01T05:00:00Z", "America/Puerto_Rico", 2026, 10)] // UTC-4 all year
        // Ahead of UTC: the facility's month has turned over, the UTC month has not.
        [InlineData("2026-10-31T11:59:00Z", "Pacific/Majuro", 2026, 10)]    // UTC+12, the widest ahead
        [InlineData("2026-10-31T12:00:00Z", "Pacific/Majuro", 2026, 11)]
        [InlineData("2026-10-31T12:00:00Z", "UTC", 2026, 10)]
        [InlineData("2026-12-31T12:00:00Z", "Pacific/Majuro", 2027, 1)]     // year rollover
        [InlineData("2026-09-30T14:00:00Z", "Pacific/Guam", 2026, 10)]      // UTC+10
        [InlineData("2026-09-30T15:00:00Z", "Pacific/Palau", 2026, 10)]     // UTC+9
        public void Resolve_reads_the_period_in_the_facilitys_timezone(string utcNow, string timeZone,
            int expectedYear, int expectedMonth)
        {
            var period = CreateResolver(utcNow).Resolve(FacilityId, timeZone);

            Assert.Equal(new ReportingPeriod(expectedYear, expectedMonth), period);
            VerifyWarnings(Times.Never());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Resolve_falls_back_to_utc_and_warns_when_the_timezone_is_blank(string? timeZone)
        {
            // Majuro is already in November at this instant, so a UTC answer is distinguishable.
            var period = CreateResolver("2026-10-31T13:00:00Z").Resolve(FacilityId, timeZone);

            Assert.Equal(new ReportingPeriod(2026, 10), period);
            VerifyWarnings(Times.Once());
        }

        [Fact]
        public void Resolve_falls_back_to_utc_and_warns_when_the_timezone_is_unusable()
        {
            var period = CreateResolver("2026-10-01T05:00:00Z").Resolve(FacilityId, "Not/AZone");

            Assert.Equal(new ReportingPeriod(2026, 10), period);
            VerifyWarnings(Times.Once());
        }

        /// <summary>
        /// The write path resolves the period before the host validates the facility, so this value can be
        /// whatever a caller sent. It must fall back rather than throw, and the resolver must not hand the
        /// value to the logger - which is why the lookup is checked rather than caught.
        /// </summary>
        [Theory]
        [InlineData("Not/AZone\r\n2026-01-01 INFO Forged log line")]
        [InlineData("../../etc/passwd")]
        [InlineData("Etc/GMT+99")]
        public void Resolve_falls_back_to_utc_for_a_timezone_a_caller_invented(string timeZone)
        {
            var period = CreateResolver("2026-10-31T13:00:00Z").Resolve(FacilityId, timeZone);

            Assert.Equal(new ReportingPeriod(2026, 10), period);
            VerifyWarnings(Times.Once());
        }

        [Fact]
        public void Resolve_never_asks_the_host_for_a_timezone()
        {
            // The write path already holds the timezone being saved; reading the stored one instead
            // would schedule a facility that changes timezone against the zone it is leaving.
            CreateResolver("2026-10-31T13:00:00Z").Resolve(FacilityId, "Pacific/Majuro");

            _timeZoneSource.VerifyNoOtherCalls();
        }

        // ---------------------------------------------------------------------------------
        // ResolveAsync - the timezone is read from the host
        // ---------------------------------------------------------------------------------

        [Fact]
        public async Task ResolveAsync_uses_the_stored_timezone()
        {
            GivenStoredTimeZone("Pacific/Majuro");

            var period = await CreateResolver("2026-10-31T13:00:00Z").ResolveAsync(FacilityId, CancellationToken.None);

            Assert.Equal(new ReportingPeriod(2026, 11), period);
            VerifyWarnings(Times.Never());
        }

        [Fact]
        public async Task ResolveAsync_passes_the_callers_token_to_the_host()
        {
            GivenStoredTimeZone("UTC");
            using var cts = new CancellationTokenSource();

            await CreateResolver("2026-10-15T12:00:00Z").ResolveAsync(FacilityId, cts.Token);

            _timeZoneSource.Verify(s => s.GetTimeZoneAsync(FacilityId, cts.Token), Times.Once());
        }

        /// <summary>
        /// A facility Link does not know is an ordinary answer on the reads - an empty list, not a 404 -
        /// so it falls back to UTC without a warning. A warning here would fire on every query for an id
        /// that simply is not there.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_falls_back_to_utc_without_warning_for_an_unknown_facility()
        {
            GivenStoredTimeZone(null);

            var period = await CreateResolver("2026-10-31T13:00:00Z").ResolveAsync(FacilityId, CancellationToken.None);

            Assert.Equal(new ReportingPeriod(2026, 10), period);
            VerifyWarnings(Times.Never());
        }

        /// <summary>
        /// A facility that exists with a blank timezone is a data problem, unlike one that does not
        /// exist at all, so it is the one that warns.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_warns_when_a_stored_facility_has_a_blank_timezone()
        {
            GivenStoredTimeZone("");

            var period = await CreateResolver("2026-10-31T13:00:00Z").ResolveAsync(FacilityId, CancellationToken.None);

            Assert.Equal(new ReportingPeriod(2026, 10), period);
            VerifyWarnings(Times.Once());
        }

        // ---------------------------------------------------------------------------------
        // The clock
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Two readings a moment apart can straddle a month boundary and mix the months. Each
        /// resolution, on every path, takes the clock exactly once.
        /// </summary>
        [Theory]
        [InlineData("Pacific/Majuro", true)]
        [InlineData("", true)]
        [InlineData("Not/AZone", true)]
        [InlineData(null, false)] // ResolveAsync for an unknown facility
        public async Task Each_resolution_reads_the_clock_once(string? timeZone, bool facilityExists)
        {
            var clock = new CountingTimeProvider(DateTimeOffset.Parse("2026-10-31T23:59:59Z"));
            var resolver = CreateResolver(clock);

            if (facilityExists)
            {
                resolver.Resolve(FacilityId, timeZone);
                Assert.Equal(1, clock.Readings);

                GivenStoredTimeZone(timeZone ?? "");
                await resolver.ResolveAsync(FacilityId, CancellationToken.None);
                Assert.Equal(2, clock.Readings);
            }
            else
            {
                GivenStoredTimeZone(null);
                await resolver.ResolveAsync(FacilityId, CancellationToken.None);
                Assert.Equal(1, clock.Readings);
            }
        }

        [Fact]
        public void Constructor_refuses_missing_dependencies()
        {
            var clock = new FakeTimeProvider();

            Assert.Throws<ArgumentNullException>(() =>
                new FacilityReportingPeriodResolver(null!, clock, _timeZoneSource.Object));
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityReportingPeriodResolver(_logger.Object, null!, _timeZoneSource.Object));
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityReportingPeriodResolver(_logger.Object, clock, null!));
        }

        private sealed class CountingTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _utcNow;

            public CountingTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

            public int Readings { get; private set; }

            public override DateTimeOffset GetUtcNow()
            {
                Readings++;
                return _utcNow;
            }
        }
    }
}
