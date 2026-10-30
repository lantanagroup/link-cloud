using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// QueryDispatch and MockDmrpApi build their context with AddSQLServerEF rather than their own
    /// AddDbContext, so the helper is the only place the audit interceptor can be attached.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class SqlServerRegistrationAuditTests
    {
        private static IReadOnlyList<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor> InterceptorsOn(
            bool auditing, bool useUpdateBaseEntityInterceptor)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration[ConfigurationConstants.AppSettings.DatabaseProvider] =
                ConfigurationConstants.AppSettings.SqlServerDatabaseProvider;
            builder.Configuration[$"ConnectionStrings:{ConfigurationConstants.DatabaseConnections.DatabaseConnection}"] =
                "Server=unused;Database=unused";

            builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();
            if (auditing)
            {
                builder.Services.AddLinkAuditing<AuditTestDbContext>();
            }
            builder.AddSQLServerEF<AuditTestDbContext>(useUpdateBaseEntityInterceptor);

            using var provider = builder.Services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AuditTestDbContext>>();

            return options.FindExtension<CoreOptionsExtension>()?.Interceptors?.ToList() ?? [];
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AServiceThatRegistersAuditing_GetsTheAuditInterceptorAttached(bool useUpdateBaseEntityInterceptor)
        {
            var interceptors = InterceptorsOn(auditing: true, useUpdateBaseEntityInterceptor);

            Assert.Single(interceptors.OfType<AuditPropertyChangeInterceptor>());
            Assert.Equal(useUpdateBaseEntityInterceptor, interceptors.OfType<UpdateBaseEntityInterceptor>().Any());
        }

        [Fact]
        public void AServiceThatDoesNotRegisterAuditing_IsLeftAsItWas()
        {
            var interceptors = InterceptorsOn(auditing: false, useUpdateBaseEntityInterceptor: true);

            Assert.Empty(interceptors.OfType<AuditPropertyChangeInterceptor>());
            Assert.Single(interceptors.OfType<UpdateBaseEntityInterceptor>());
        }
    }
}
