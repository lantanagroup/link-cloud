using System.Collections.Concurrent;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// The startup line is where "was this supposed to be masked?" gets answered, so it has to
    /// name everything it masks - including the ones an entity declares with <see cref="AuditMaskAttribute"/>,
    /// which no option lists.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class AuditStartupLogTests
    {
        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            public ConcurrentQueue<string> Messages { get; } = new();

            public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

            public void Dispose()
            {
            }

            private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                    Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
            }
        }

        private static async Task<string> TheMaskingLine(Action<IServiceCollection> registerAuditing)
        {
            var logs = new CapturingLoggerProvider();
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.AddProvider(logs));
            services.AddMetrics();
            services.AddSingleton(AuditTest.Service());
            services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
            registerAuditing(services);
            services.AddScoped<IAuditPublisher, FakeAuditPublisher>();

            await using var provider = services.BuildServiceProvider();
            await provider.GetServices<IHostedService>().OfType<AuditStartupLogger>().Single()
                .StartAsync(CancellationToken.None);

            return Assert.Single(logs.Messages, message => message.StartsWith("Audit masking for"));
        }

        [Fact]
        public async Task TheStartupLine_NamesThePropertiesAnEntityMarksAuditMask()
        {
            var line = await TheMaskingLine(services =>
            {
                //building the model does not open a connection, so none is needed here.
                services.AddDbContext<AuditTestDbContext>(options => options.UseSqlite("DataSource=:memory:"));
                services.AddLinkAuditing<AuditTestDbContext>();
            });

            Assert.Contains("attributes=[TestVendor.ContactEmail]", line);
        }

        [Fact]
        public async Task TheStartupLine_ListsWhatIsMaskedAndWhatIsSkipped()
        {
            var line = await TheMaskingLine(services => services.AddLinkAuditing());

            //masked first - names, patterns, attributes - then the timestamps skipped as noise. There
            //is no list of entity types: a property is masked, never a whole entity dropped.
            Assert.Matches(@"^Audit masking for \S+: names=\[.+\] patterns=\[.+\] attributes=\[.*\] skipped timestamps=\[.+\]$", line);
        }

        private sealed class UnbuildableDbContext(DbContextOptions<UnbuildableDbContext> options) : DbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder) =>
                throw new InvalidOperationException("this model cannot be built");
        }

        [Fact]
        public async Task AModelThatCannotBeBuilt_SaysSoRatherThanListingNone()
        {
            var line = await TheMaskingLine(services =>
            {
                services.AddDbContext<UnbuildableDbContext>(options => options.UseSqlite("DataSource=:memory:"));
                services.AddLinkAuditing<UnbuildableDbContext>();
            });

            //an empty list would read as "nothing is masked by attribute", which nobody checked.
            Assert.Contains("attributes=[unreadable]", line);
        }

        [Fact]
        public async Task AServiceWithNoDatabase_ListsNoAttributeMasks()
        {
            var line = await TheMaskingLine(services => services.AddLinkAuditing());

            Assert.Contains("attributes=[]", line);
        }
    }
}
