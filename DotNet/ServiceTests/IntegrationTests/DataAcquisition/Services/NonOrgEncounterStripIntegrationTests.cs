using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis.Extensions.Core.Abstractions;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.System.Text.Json;
using Testcontainers.Redis;
using RedisExtensionsConfiguration = StackExchange.Redis.Extensions.Core.Configuration.RedisConfiguration;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Services;

/// <summary>
/// The non-org encounter strip driven end to end: real org-location conditions in SQL, real mapping
/// evaluation, and a real Redis holding the correlation's encounters.
/// </summary>
/// <remarks>
/// No other suite reaches the whole: the unit tests mock the cache away, and
/// <c>RedisResourceCacheReplaceTests</c> calls <c>ReplaceResourcesAsync</c> directly with a survivor list it
/// picked itself. Here the survivors are whatever the facility's FhirPath conditions decide, which is what
/// the strip actually depends on.
/// <para>
/// A patient with one org and one non-org encounter is the case the local E2E stack cannot produce: the
/// generator emits one Encounter per patient, so a generated correlation is all-org or all-non-org, and an
/// all-non-org correlation strips to nothing and takes the key-delete path rather than the replace. See
/// docs-dev/resource-cache.md.
/// </para>
/// </remarks>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class NonOrgEncounterStripIntegrationTests : IAsyncLifetime
{
    // Matches the critical-care unit and nothing else. The hospital it is part of does not match, so
    // nothing propagates down to the pharmacy beside it -- which is what leaves one encounter org and one
    // not.
    private const string IcuOnlyFhirPath =
        "type.coding.where(system='http://terminology.hl7.org/CodeSystem/v3-RoleCode' and code='ICU').exists()";

    private readonly DataAcquisitionIntegrationTestFixture _fixture;
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:latest").Build();

    private RedisConnectionPoolManager _pool = null!;
    private IRedisDatabase _redisDatabase = null!;
    private RedisResourceCache _resourceCache = null!;

    public NonOrgEncounterStripIntegrationTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();

        var configuration = new RedisExtensionsConfiguration { ConnectionString = _redis.GetConnectionString() };
        _pool = new RedisConnectionPoolManager(configuration);
        _redisDatabase = new RedisDatabase(
            _pool,
            new SystemTextJsonSerializer(),
            configuration.ServerEnumerationStrategy,
            configuration.Database,
            configuration.MaxValueLength);

        _resourceCache = new RedisResourceCache(
            _redisDatabase,
            Options.Create(new ResourceCacheSettings
            {
                Redis = new ResourceCacheRedisSettings { CacheEntryTtlDays = 7 }
            }),
            Mock.Of<ILogger<RedisResourceCache>>());
    }

    public async Task DisposeAsync()
    {
        _pool?.Dispose();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task StripNonOrgEncountersFromCacheAsync_MixedEncounters_LeavesOnlyTheOrgEncounter()
    {
        using var context = await ArrangeAsync(encountersAtIcu: ["enc-icu"], encountersAtPharmacy: ["enc-pharmacy"]);

        var outcome = await context.Service.StripNonOrgEncountersFromCacheAsync(
            context.FacilityId, context.CorrelationId, context.PatientId);

        Assert.Equal(LocationOrgStatus.Found, outcome.Status);
        Assert.Equal(2, outcome.EncounterCount);
        Assert.Equal(1, outcome.OrgEncounterCount);

        var remaining = await _resourceCache.GetAsync(context.CacheKey);

        Assert.Single(remaining);
        Assert.Equal("enc-icu", remaining[0].Id);

        // The entry survives the replace, so it must still be bounded. A replaced key that lost its
        // lifetime would sit in Redis until something evicted it.
        var ttl = await _redisDatabase.Database.KeyTimeToLiveAsync(context.CacheKey);
        Assert.NotNull(ttl);
    }

    [Fact]
    public async Task StripNonOrgEncountersFromCacheAsync_NoOrgEncounters_RemovesTheEntry()
    {
        using var context = await ArrangeAsync(
            encountersAtIcu: [],
            encountersAtPharmacy: ["enc-pharmacy-1", "enc-pharmacy-2"]);

        var outcome = await context.Service.StripNonOrgEncountersFromCacheAsync(
            context.FacilityId, context.CorrelationId, context.PatientId);

        Assert.Equal(LocationOrgStatus.NotFound, outcome.Status);
        Assert.Equal(0, outcome.OrgEncounterCount);

        // An empty entry reads differently from an absent one: empty is served downstream as "this
        // correlation has no encounters", absent falls through to durable storage. So the key goes rather
        // than being emptied.
        Assert.False(await _redisDatabase.Database.KeyExistsAsync(context.CacheKey));
    }

    [Fact]
    public async Task StripNonOrgEncountersFromCacheAsync_AllOrgEncounters_LeavesTheCacheAlone()
    {
        using var context = await ArrangeAsync(
            encountersAtIcu: ["enc-icu-1", "enc-icu-2"],
            encountersAtPharmacy: []);

        var outcome = await context.Service.StripNonOrgEncountersFromCacheAsync(
            context.FacilityId, context.CorrelationId, context.PatientId);

        Assert.Equal(LocationOrgStatus.Found, outcome.Status);
        Assert.Equal(2, outcome.OrgEncounterCount);

        var remaining = await _resourceCache.GetAsync(context.CacheKey);
        Assert.Equal(2, remaining.Count);
    }

    /// <summary>
    /// Configures a facility with the ICU-only condition, runs the real location and encounter mapping
    /// evaluation over a hospital / ICU / pharmacy hierarchy, and loads the correlation's encounters into
    /// Redis the way acquisition would.
    /// </summary>
    private async Task<StripContext> ArrangeAsync(string[] encountersAtIcu, string[] encountersAtPharmacy)
    {
        var facilityId = $"Fac_{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid().ToString();
        const string patientId = "strip-patient";

        var scope = _fixture.ServiceProvider.CreateScope();

        // EncounterMappingManager refuses a facility it has never seen and a patient that facility never
        // acquired, so the rows acquisition would already have written come first.
        await scope.ServiceProvider
            .GetRequiredService<IEntityRepository<FhirQueryConfiguration>>()
            .AddAsync(new FhirQueryConfiguration
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                FhirServerBaseUrl = "http://localhost/fhir"
            });

        await scope.ServiceProvider
            .GetRequiredService<IEntityRepository<DataAcquisitionLog>>()
            .AddAsync(new DataAcquisitionLog
            {
                FacilityId = facilityId,
                PatientId = patientId,
                CorrelationId = correlationId
            });

        var configManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        await configManager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = facilityId,
            Description = "Critical care units only",
            IsActive = true,
            Conditions = [new CreateOrganizationLocationConditionModel { FhirPath = IcuOnlyFhirPath, Priority = 1 }]
        });

        var service = new LocationMappingService(
            scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>(),
            scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>(),
            scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>(),
            scope.ServiceProvider.GetRequiredService<IEncounterMappingQueries>(),
            scope.ServiceProvider.GetRequiredService<IEncounterMappingManager>(),
            new Mock<IReferenceResourcesQueries>().Object,
            scope.ServiceProvider.GetRequiredService<ICacheService>(),
            _resourceCache,
            Mock.Of<ILogger<LocationMappingService>>());

        // The hospital first, so the units below it resolve their PartOf the way acquisition does.
        await service.UpdateLocationMappingAsync(facilityId, Hospital());
        await service.UpdateLocationMappingAsync(facilityId, Unit("icu", "ICU", "Medical ICU"));
        await service.UpdateLocationMappingAsync(facilityId, Unit("pharmacy", "PHARM", "Inpatient Pharmacy"));

        var encounters = encountersAtIcu
            .Select(id => NewEncounter(id, patientId, "icu"))
            .Concat(encountersAtPharmacy.Select(id => NewEncounter(id, patientId, "pharmacy")))
            .ToList();

        foreach (var encounter in encounters)
        {
            await service.UpdateEncounterLocationMappingAsync(facilityId, encounter);
        }

        var cacheKey = $"{correlationId}:{ResourceType.Encounter}";
        await _resourceCache.AppendResourcesAsync(
            cacheKey, encounters.Cast<DomainResource>().ToList(), ResourceType.Encounter);

        return new StripContext(scope, service, facilityId, correlationId, patientId, cacheKey);
    }

    private static Location Hospital() => new()
    {
        Id = "hospital",
        Status = Location.LocationStatus.Active,
        Name = "Main Hospital"
    };

    private static Location Unit(string id, string roleCode, string name) => new()
    {
        Id = id,
        Status = Location.LocationStatus.Active,
        Name = name,
        Type =
        [
            new CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-RoleCode", roleCode, name, null)
        ],
        PartOf = new ResourceReference("Location/hospital")
    };

    private static Encounter NewEncounter(string id, string patientId, string locationId) => new()
    {
        Id = id,
        Status = Encounter.EncounterStatus.Finished,
        Subject = new ResourceReference($"Patient/{patientId}"),
        Location =
        [
            new Encounter.LocationComponent { Location = new ResourceReference($"Location/{locationId}") }
        ]
    };

    private sealed record StripContext(
        IServiceScope Scope,
        ILocationMappingService Service,
        string FacilityId,
        string CorrelationId,
        string PatientId,
        string CacheKey) : IDisposable
    {
        public void Dispose() => Scope.Dispose();
    }
}
