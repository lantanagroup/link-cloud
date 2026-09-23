using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.MsSql;
using Xunit;
using ResourceType = LantanaGroup.Link.Normalization.Domain.Entities.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization;

/// <summary>
/// SQL Server proof for operation-sequence cache invalidation and lock ordering.
/// The unit suite uses SQLite, which locks the database file and sorts Guids differently.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class OperationSequenceSqlServerTests
{
    private const string CopyJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}";

    private readonly NormalizationSqlServerFixture _fixture;

    public OperationSequenceSqlServerTests(NormalizationSqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteOnOneConnection_DropsTheOtherReplicaCache()
    {
        var facilityId = "sql-" + Guid.NewGuid().ToString("N");
        await using var writer = _fixture.Open();
        await using var reader = _fixture.Open();
        var operationId = await writer.SeedAsync(facilityId, "Copy");
        await writer.Manager.CreateOperationSequences(Sequence(facilityId, operationId));

        var cached = Assert.Single(await reader.Queries.Search(Typed(facilityId)));
        Assert.Equal(operationId, cached.OperationResourceType.OperationId);

        Assert.True(await writer.Manager.DeleteOperationSequence(new DeleteOperationSequencesModel { FacilityId = facilityId }));

        Assert.Empty(await reader.Queries.Search(Typed(facilityId)));
        Assert.Empty(await reader.Context.OperationSequences.Where(sequence => sequence.FacilityId == facilityId).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentReplaceAndDelete_FinishWithoutADeadlock()
    {
        var facilityId = "sql-" + Guid.NewGuid().ToString("N");
        await using var setup = _fixture.Open();
        var first = await setup.SeedAsync(facilityId, "First");
        var second = await setup.SeedAsync(facilityId, "Second");
        await setup.Manager.CreateOperationSequences(Sequence(facilityId, first));

        await using var deleter = _fixture.Open();
        await using var replacer = _fixture.Open();
        var delete = deleter.Manager.DeleteOperation(new DeleteOperationModel { FacilityId = facilityId });
        var replace = replacer.Manager.CreateOperationSequences(Sequence(facilityId, second));

        Exception? failure = null;
        try
        {
            await Task.WhenAll(delete, replace).WaitAsync(TimeSpan.FromSeconds(40));
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        Assert.False(ContainsDeadlock(failure), failure?.ToString());
        await using var check = _fixture.Open();
        var remaining = await check.Context.Operations.CountAsync(operation => operation.FacilityId == facilityId);
        var sequences = await check.Context.OperationSequences.CountAsync(sequence => sequence.FacilityId == facilityId);
        Assert.True(remaining == 0 || sequences <= remaining);
    }

    [Fact]
    public async Task ResourceLock_UsesThePersistedNameOnSqlServer()
    {
        var facilityId = "sql-" + Guid.NewGuid().ToString("N");
        await using var session = _fixture.Open();
        var operationId = await session.SeedAsync(facilityId, "Copy");
        var names = await session.Queries.CanonicalResourceNamesAsync(["pAtIeNt"]);

        Assert.Equal(["Patient"], names);
        var updated = await session.Manager.UpdateOperation(new UpdateOperationModel
        {
            Id = operationId,
            FacilityId = facilityId,
            Name = "Copy",
            Description = "Copy",
            OperationJson = CopyJson,
            ResourceTypes = ["pAtIeNt"]
        });

        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        var stored = await session.Context.OperationResourceTypes
            .Where(map => map.OperationId == operationId)
            .Select(map => map.ResourceType.Name)
            .SingleAsync();
        Assert.Equal("Patient", stored);
    }

    private static bool ContainsDeadlock(Exception? exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is AggregateException aggregate && aggregate.InnerExceptions.Any(ContainsDeadlock))
            {
                return true;
            }

            if (current is SqlException sql && sql.Number == 1205)
            {
                return true;
            }

            if (current.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static OperationSequenceSearchModel Typed(string facilityId) => new() { FacilityId = facilityId, ResourceType = "Patient" };

    private static CreateOperationSequencesModel Sequence(string facilityId, params Guid[] operationIds)
    {
        return new CreateOperationSequencesModel
        {
            FacilityId = facilityId,
            ResourceType = "Patient",
            OperationSequences = operationIds.Select((id, index) => new CreateOperationSequenceModel
            {
                OperationId = id,
                Sequence = index + 1
            }).ToList()
        };
    }
}

[CollectionDefinition(SqlServerCollection.Name)]
public sealed class SqlServerCollection : ICollectionFixture<NormalizationSqlServerFixture>
{
    public const string Name = "Normalization SQL Server";
}

public sealed class NormalizationSqlServerFixture : IAsyncLifetime
{
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04";
    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder(SqlServerImage).Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        var options = new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlServer(ConnectionString).Options;
        await using var context = new NormalizationDbContext(options);
        await context.Database.MigrateAsync();
    }

    public Session Open() => new(ConnectionString);

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    public sealed class Session : IAsyncDisposable
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public Session(string connectionString)
        {
            var options = new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlServer(connectionString).Options;
            Context = new NormalizationDbContext(options);
            var database = new Database(
                Context,
                new OperationRepository(Context),
                new OperationSequenceRepository(Context),
                new ResourceTypeRepository(Context),
                new OperationResourceTypeRepository(Context),
                new VendorVersionOperationPresetRepository(Context));
            var resolver = new Mock<IVendorVersionResolver>();
            resolver.Setup(service => service.ResolveAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<Guid> ids, CancellationToken _) =>
                    Task.FromResult<IReadOnlyDictionary<Guid, VendorVersionModel>>(ids.Distinct().ToDictionary(
                        id => id,
                        id => new VendorVersionModel { Id = id, VendorId = Guid.Empty, Version = "test" })));
            var resources = new ResourceQueries(Context);
            Queries = new OperationSequenceQueries(database, Context, _cache, resolver.Object);
            var operations = new OperationQueries(database, Context, resolver.Object);
            Manager = new OperationManager(
                database,
                operations,
                Queries,
                resources,
                new ResourceManager(database, resources, Queries, NullLogger<ResourceManager>.Instance),
                resolver.Object,
                Mock.Of<IHSLOCQueries>());
        }

        public NormalizationDbContext Context { get; }
        public OperationSequenceQueries Queries { get; }
        public OperationManager Manager { get; }

        public async Task<Guid> SeedAsync(string facilityId, string name)
        {
            var resource = await Context.ResourceTypes.SingleOrDefaultAsync(candidate => candidate.Name == "Patient");
            if (resource == null)
            {
                resource = new ResourceType { Id = Guid.NewGuid(), Name = "Patient" };
                Context.ResourceTypes.Add(resource);
            }

            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                Name = name,
                Description = name,
                OperationType = "CopyProperty",
                OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
                CreateDate = DateTime.UtcNow
            };
            Context.Operations.Add(operation);
            Context.OperationResourceTypes.Add(new OperationResourceType
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                ResourceTypeId = resource.Id
            });
            await Context.SaveChangesAsync();
            return operation.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _cache.Dispose();
        }
    }
}
