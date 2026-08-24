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
    /// The reporting plan source is what turns stored rows into the measures a facility is enrolled to
    /// report. Its filters run as SQL against a <see cref="TenantDbContext"/> here rather than against a
    /// mocked repository that would accept any predicate.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class DbBackedReportingPlanSourceTests : IDisposable
    {
        private const string FacilityId = "100";
        private const int Month = 5;
        private const int Year = 2026;

        private readonly SqliteConnection _connection;

        public DbBackedReportingPlanSourceTests()
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

        private static DbBackedReportingPlanSource CreateSource(TenantDbContext context) =>
            new(NullLogger<DbBackedReportingPlanSource>.Instance,
                new EntityRepository<FacilityReportingPlan, TenantDbContext>(context),
                new EntityRepository<MeasureMapping, TenantDbContext>(context));

        private static MeasureMapping AddMapping(TenantDbContext context, string measure, string dqm,
            Frequency frequency)
        {
            var mapping = new MeasureMapping
            {
                Measure = measure,
                DQM = dqm,
                Frequency = frequency
            };

            context.MeasureMappings.Add(mapping);
            return mapping;
        }

        private static void AddPlan(TenantDbContext context, MeasureMapping mapping,
            string facilityId = FacilityId, int month = Month, int year = Year, bool isReporting = true)
        {
            context.FacilityReportingPlans.Add(new FacilityReportingPlan
            {
                FacilityId = facilityId,
                MeasureMappingId = mapping.Id,
                ReportingMonth = month,
                ReportingYear = year,
                IsReporting = isReporting
            });
        }

        [Fact]
        public async Task Resolves_each_plan_through_its_measure_mapping()
        {
            await using var context = CreateContext();

            var hob = AddMapping(context, "HOB", "NHSNAcuteCareHospitalMonthlyInitialPopulation", Frequency.Monthly);
            var htcdi = AddMapping(context, "HTCDI", "NHSNAcuteCareHospitalDailyInitialPopulation", Frequency.Daily);
            AddPlan(context, hob);
            AddPlan(context, htcdi);
            await context.SaveChangesAsync();

            var entries = await CreateSource(context).GetForPeriodAsync(FacilityId, Month, Year);

            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.Measure == "HOB"
                && e.DQM == "NHSNAcuteCareHospitalMonthlyInitialPopulation"
                && e.Frequency == Frequency.Monthly);
            Assert.Contains(entries, e => e.Measure == "HTCDI"
                && e.DQM == "NHSNAcuteCareHospitalDailyInitialPopulation"
                && e.Frequency == Frequency.Daily);
        }

        /// <summary>
        /// A measure the facility has stopped reporting stays in the table with IsReporting cleared, so
        /// the row is history rather than an enrollment.
        /// </summary>
        [Fact]
        public async Task Excludes_measures_the_facility_is_not_reporting()
        {
            await using var context = CreateContext();

            var reporting = AddMapping(context, "HOB", "dqm-monthly", Frequency.Monthly);
            var notReporting = AddMapping(context, "HTCDI", "dqm-daily", Frequency.Daily);
            AddPlan(context, reporting);
            AddPlan(context, notReporting, isReporting: false);
            await context.SaveChangesAsync();

            var entries = await CreateSource(context).GetForPeriodAsync(FacilityId, Month, Year);

            var entry = Assert.Single(entries);
            Assert.Equal("HOB", entry.Measure);
        }

        [Theory]
        [InlineData("200", Month, Year)]
        [InlineData(FacilityId, 6, Year)]
        [InlineData(FacilityId, Month, 2027)]
        public async Task Returns_nothing_outside_the_requested_facility_and_period(string facilityId, int month,
            int year)
        {
            await using var context = CreateContext();

            var mapping = AddMapping(context, "HOB", "dqm-monthly", Frequency.Monthly);
            AddPlan(context, mapping);
            await context.SaveChangesAsync();

            var entries = await CreateSource(context).GetForPeriodAsync(facilityId, month, year);

            Assert.Empty(entries);
        }

        /// <summary>
        /// The scheduling workflow records a measure DMRP returned that Link has no mapping for with an
        /// empty dQM. The source hands it on rather than dropping it, so the caller can say so.
        /// </summary>
        [Fact]
        public async Task Returns_an_unmapped_measure_with_no_dqm()
        {
            await using var context = CreateContext();

            var unmapped = AddMapping(context, "NEWMEASURE", string.Empty, Frequency.Adhoc);
            AddPlan(context, unmapped);
            await context.SaveChangesAsync();

            var entries = await CreateSource(context).GetForPeriodAsync(FacilityId, Month, Year);

            var entry = Assert.Single(entries);
            Assert.Equal("NEWMEASURE", entry.Measure);
            Assert.Equal(string.Empty, entry.DQM);
        }

        [Fact]
        public async Task Returns_nothing_for_a_facility_with_no_plans()
        {
            await using var context = CreateContext();

            var entries = await CreateSource(context).GetForPeriodAsync(FacilityId, Month, Year);

            Assert.Empty(entries);
        }
    }
}
