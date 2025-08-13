using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Repositories;
using LantanaGroup.Link.Census.Application.Repositories.Scheduling;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census
{
    [CollectionDefinition("CensusIntegrationTests")]
    public class DatabaseCollection : ICollectionFixture<CensusIntegrationTestFixture> { }

    public class CensusIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;

        public CensusIntegrationTestFixture()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddDbContext<CensusContext>(options =>
                    {
                        options.UseInMemoryDatabase("CensusTestDatabase");
                        options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                    });
                    services.AddScoped<IPatientEventManager, PatientEventManager>();
                    services.AddScoped<IPatientEventQueries, PatientEventQueries>();
                    services.AddScoped<IPatientEncounterManager, PatientEncounterManager>();
                    services.AddScoped<IPatientEncounterQueries, PatientEncounterQueries>();
                    services.AddScoped<ICensusConfigManager, CensusConfigManager>();
                    services.AddScoped<IBaseEntityRepository<CensusConfigEntity>, CensusEntityRepository<CensusConfigEntity>>();
                    services.AddScoped<IBaseEntityRepository<PatientEncounter>, CensusEntityRepository<PatientEncounter>>();
                    services.AddScoped<IBaseEntityRepository<PatientEvent>, CensusEntityRepository<PatientEvent>>();
                    services.AddScoped<IBaseEntityRepository<PatientIdentifier>, CensusEntityRepository<PatientIdentifier>>();
                    services.AddScoped<IBaseEntityRepository<PatientVisitIdentifier>, CensusEntityRepository<PatientVisitIdentifier>>();
                    services.AddScoped<ICensusSchedulingRepository, CensusSchedulingRepository>();
                    services.AddSingleton<ICensusServiceMetrics, NullCensusServiceMetrics>();
                    services.AddSingleton<ITenantApiService, NullTenantApiService>();
                    services.AddQuartz();
                    services.AddQuartzHostedService();
                })
                .Build();
        
            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;
            using var scope = ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CensusContext>();
            db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }
    }

    public class NullCensusServiceMetrics : ICensusServiceMetrics
    {
        public void IncrementPatientAdmittedCounter(List<KeyValuePair<string, object?>> tags) { }
        public void IncrementPatientDischargedCounter(List<KeyValuePair<string, object?>> tags) { }
    }

    public class NullTenantApiService : ITenantApiService
    {
        public Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task<FacilityConfig> GetFacilityConfig(string facilityId, CancellationToken cancellationToken = default)
            => Task.FromResult(new FacilityConfig { FacilityId = facilityId });
    }
}
