using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// A vendor with a credential, a personal detail and a foreign key, so one save can show
    /// everything the interceptor is supposed to leave out.
    /// </summary>
    internal class TestVendor : BaseEntityExtended
    {
        public string Name { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public string SigningKey { get; set; } = string.Empty;

        [AuditMask]
        public string ContactEmail { get; set; } = string.Empty;

        public string? TenantId { get; set; }

        public TestTenant? Tenant { get; set; }
    }

    internal class TestTenant : BaseEntityExtended
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// An entity whose interceptor stamps <c>LastModifiedOn</c> rather than BaseEntityExtended's
    /// <c>ModifyDate</c>, the way Account's does, plus a timestamp no shared default names.
    /// </summary>
    internal class TestAccountUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsActive { get; set; } = true;

        public DateTime? LastModifiedOn { get; set; }

        public DateTime? StampedAt { get; set; }
    }

    internal sealed class TestCredentials
    {
        public string AuthType { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;
    }

    internal sealed class TestSchedule
    {
        public string Event { get; set; } = string.Empty;

        public string Duration { get; set; } = string.Empty;
    }

    /// <summary>
    /// A list stored as one JSON column through a value converter, the way QueryDispatch stores
    /// DispatchSchedules.
    /// </summary>
    internal class TestDispatchConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public List<TestSchedule> Schedules { get; set; } = [];

        /// <summary>
        /// Credentials stored as one JSON column, the way Data Acquisition stores its
        /// AuthenticationConfiguration in a column named Authentication.
        /// </summary>
        public TestCredentials? Authentication { get; set; }
    }

    /// <summary>
    /// A list mapped with no converter of its own, the way Data Acquisition maps
    /// <c>FhirQuery.QueryParameters</c> and Notification its <c>Recipients</c> - Entity Framework
    /// stores it as a JSON column without a value converter - plus the value kinds whose text
    /// depends on how they are asked for it.
    /// </summary>
    internal class TestFhirQuery
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public List<string> QueryParameters { get; set; } = [];

        public byte[]? Fingerprint { get; set; }

        public decimal Threshold { get; set; }

        public DateTime? EffectiveFrom { get; set; }
    }

    internal sealed class AuditTestDbContext(DbContextOptions<AuditTestDbContext> options) : DbContext(options)
    {
        public DbSet<TestVendor> Vendors => Set<TestVendor>();

        public DbSet<TestTenant> Tenants => Set<TestTenant>();

        public DbSet<TestAccountUser> AccountUsers => Set<TestAccountUser>();

        public DbSet<TestDispatchConfiguration> DispatchConfigurations => Set<TestDispatchConfiguration>();

        public DbSet<TestFhirQuery> FhirQueries => Set<TestFhirQuery>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestDispatchConfiguration>()
                .Property(configuration => configuration.Schedules)
                .HasConversion(
                    schedules => System.Text.Json.JsonSerializer.Serialize(schedules, (System.Text.Json.JsonSerializerOptions?)null),
                    json => System.Text.Json.JsonSerializer.Deserialize<List<TestSchedule>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<TestSchedule>());

            modelBuilder.Entity<TestDispatchConfiguration>()
                .Property(configuration => configuration.Authentication)
                .HasConversion(
                    credentials => System.Text.Json.JsonSerializer.Serialize(credentials, (System.Text.Json.JsonSerializerOptions?)null),
                    json => System.Text.Json.JsonSerializer.Deserialize<TestCredentials>(json, (System.Text.Json.JsonSerializerOptions?)null));
        }
    }

    /// <summary>
    /// Sqlite rather than the in-memory provider, because the interceptor reads relational metadata
    /// - which properties are keys - and skips them on that basis.
    /// </summary>
    internal sealed class AuditInterceptorFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public AuditInterceptorFixture(AuditPropertyChangeInterceptor interceptor)
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AuditTestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptor)
                .Options;

            Context = new AuditTestDbContext(options);
            Context.Database.EnsureCreated();
        }

        public AuditTestDbContext Context { get; }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
