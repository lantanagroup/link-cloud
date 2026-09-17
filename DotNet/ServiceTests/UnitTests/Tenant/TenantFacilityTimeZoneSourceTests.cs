using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Tenant.Business;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant
{
    /// <summary>
    /// The host's answer to "where is this facility", which the DMRP module reads its reporting period
    /// from. Run against a real <see cref="TenantDbContext"/>, because the distinction that matters - no
    /// facility at all versus a facility with a blank timezone - is decided by the query.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class TenantFacilityTimeZoneSourceTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public TenantFacilityTimeZoneSourceTests()
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

        private static TenantFacilityTimeZoneSource CreateSource(TenantDbContext context) =>
            new(new EntityRepository<Facility, TenantDbContext>(context));

        private static async Task AddFacilityAsync(TenantDbContext context, string facilityId, string timeZone,
            bool isDeleted = false)
        {
            context.Facilities.Add(new Facility
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                FacilityName = facilityId,
                TimeZone = timeZone,
                IsDeleted = isDeleted,
                ScheduledReports = new ScheduledReportModel { Daily = [], Weekly = [], Monthly = [] }
            });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Returns_the_facilitys_stored_timezone()
        {
            using var context = CreateContext();
            await AddFacilityAsync(context, "MAJURO", "Pacific/Majuro");

            var timeZone = await CreateSource(context).GetTimeZoneAsync("MAJURO");

            Assert.Equal("Pacific/Majuro", timeZone);
        }

        [Fact]
        public async Task Returns_only_the_requested_facilitys_timezone()
        {
            using var context = CreateContext();
            await AddFacilityAsync(context, "MAJURO", "Pacific/Majuro");
            await AddFacilityAsync(context, "PAGO-PAGO", "Pacific/Pago_Pago");

            var timeZone = await CreateSource(context).GetTimeZoneAsync("PAGO-PAGO");

            Assert.Equal("Pacific/Pago_Pago", timeZone);
        }

        /// <summary>
        /// Null is the module's signal that Link has no such facility, which the reads treat as an
        /// ordinary empty answer and log quietly.
        /// </summary>
        [Fact]
        public async Task Returns_null_for_an_id_no_facility_has()
        {
            using var context = CreateContext();
            await AddFacilityAsync(context, "MAJURO", "Pacific/Majuro");

            var timeZone = await CreateSource(context).GetTimeZoneAsync("NO-SUCH-FACILITY");

            Assert.Null(timeZone);
        }

        /// <summary>
        /// A blank timezone on a facility that exists is a data problem the module warns about. Collapsing
        /// it to null would disguise it as a facility that does not exist, and the warning would never fire.
        /// </summary>
        [Fact]
        public async Task Returns_a_blank_timezone_as_blank_rather_than_null()
        {
            using var context = CreateContext();
            await AddFacilityAsync(context, "BLANK", "");

            var timeZone = await CreateSource(context).GetTimeZoneAsync("BLANK");

            Assert.Equal(string.Empty, timeZone);
        }

        /// <summary>
        /// A soft-deleted facility keeps its reporting plans and they stay readable. Where it is does not
        /// change when it is deactivated, so its history keeps being read in its own timezone rather than
        /// quietly moving to UTC.
        /// </summary>
        [Fact]
        public async Task Returns_the_timezone_of_a_soft_deleted_facility()
        {
            using var context = CreateContext();
            await AddFacilityAsync(context, "GUAM", "Pacific/Guam", isDeleted: true);

            var timeZone = await CreateSource(context).GetTimeZoneAsync("GUAM");

            Assert.Equal("Pacific/Guam", timeZone);
        }

        [Fact]
        public void Constructor_refuses_a_missing_repository()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TenantFacilityTimeZoneSource(null!));

            Assert.Equal("facilityRepository", exception.ParamName);
        }
    }
}
