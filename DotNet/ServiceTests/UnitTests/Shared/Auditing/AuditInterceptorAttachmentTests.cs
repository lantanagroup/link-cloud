using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// AddLinkAuditInterceptor is the one line a service writes inside its AddDbContext. It has to
    /// be safe in setup code shared with a process that never registers auditing - Data
    /// Acquisition's worker runs the same RegisterEntityFramework as its API.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class AuditInterceptorAttachmentTests
    {
        private static ServiceProvider Services(bool auditing)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            if (auditing)
            {
                services.AddLinkAuditing<AuditTestDbContext>();
            }
            services.AddDbContext<AuditTestDbContext>((sp, options) =>
                options.UseSqlite("DataSource=:memory:").AddLinkAuditInterceptor(sp));

            return services.BuildServiceProvider();
        }

        private static IReadOnlyList<IInterceptor> InterceptorsIn(IServiceScope scope) =>
            scope.ServiceProvider.GetRequiredService<DbContextOptions<AuditTestDbContext>>()
                .FindExtension<CoreOptionsExtension>()?.Interceptors?.ToList() ?? [];

        [Fact]
        public void WhenAuditingIsRegistered_TheInterceptorIsAttached()
        {
            using var provider = Services(auditing: true);
            using var scope = provider.CreateScope();

            Assert.Single(InterceptorsIn(scope).OfType<AuditPropertyChangeInterceptor>());
        }

        [Fact]
        public void WhenAuditingIsNotRegistered_NothingIsAttachedAndNothingFails()
        {
            using var provider = Services(auditing: false);
            using var scope = provider.CreateScope();

            Assert.Empty(InterceptorsIn(scope).OfType<AuditPropertyChangeInterceptor>());

            //the worker's case: the context still builds and works.
            using var context = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
            context.Database.OpenConnection();
            Assert.True(context.Database.EnsureCreated());
        }

        [Fact]
        public void EachRequest_GetsItsOwnInterceptor()
        {
            //the interceptor holds the request's captured changes, so sharing one across requests
            //would mix them up.
            using var provider = Services(auditing: true);
            using var first = provider.CreateScope();
            using var second = provider.CreateScope();

            Assert.NotSame(
                InterceptorsIn(first).OfType<AuditPropertyChangeInterceptor>().Single(),
                InterceptorsIn(second).OfType<AuditPropertyChangeInterceptor>().Single());
        }

        [Fact]
        public void TheInterceptorAttached_WritesToTheSameRequestsAuditContext()
        {
            using var provider = Services(auditing: true);
            using var scope = provider.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
            //what the audit filter does when the request begins.
            audit.IsRecording = true;

            using var context = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            var vendor = new TestVendor { Name = "Epic" };
            context.Vendors.Add(vendor);
            context.SaveChanges();

            vendor.Name = "Cerner";
            context.SaveChanges();

            Assert.Equal("Cerner", Assert.Single(audit.PropertyChanges).NewPropertyValue);
        }

        [Fact]
        public void NullArguments_AreRejected()
        {
            using var provider = Services(auditing: true);

            Assert.Throws<ArgumentNullException>(() =>
                AuditingRegistrationExtensions.AddLinkAuditInterceptor(null!, provider));
            Assert.Throws<ArgumentNullException>(() =>
                new DbContextOptionsBuilder().AddLinkAuditInterceptor(null!));
        }
    }
}
