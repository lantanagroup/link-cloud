using System.Collections.Concurrent;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization;

/// <summary>
/// Parallel facility creates match CI: each request has its own connection and an empty
/// vendor-version list, while other requests search those facilities. SQL Server must
/// finish both without error 1205.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class CreateOperationConcurrencySqlServerTests
{
    private const string CopyJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}";
    private const int ParallelCreates = 32;

    private readonly NormalizationSqlServerFixture _fixture;

    public CreateOperationConcurrencySqlServerTests(NormalizationSqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAndSearchFacilityOperations_InParallel_DoesNotDeadlock()
    {
        await SeedResourceTypesAsync();

        var facilityIds = Enumerable.Range(0, ParallelCreates)
            .Select(_ => "parallel-" + Guid.NewGuid().ToString("N"))
            .ToArray();
        var failures = new ConcurrentBag<string>();
        var created = 0;
        var searches = 0;
        var pendingCreates = ParallelCreates;

        var createTasks = Enumerable.Range(0, ParallelCreates).Select(async index =>
        {
            try
            {
                await using var session = _fixture.Open();
                var result = await session.Manager.CreateOperation(new CreateOperationModel
                {
                    FacilityId = facilityIds[index],
                    OperationType = "CopyProperty",
                    OperationJson = CopyJson,
                    ResourceTypes = ["Patient", "Location"],
                    Name = "Parallel " + index,
                    Description = "parallel create",
                    VendorVersionIds = []
                });

                if (!result.IsSuccess || result.ObjectResult is not OperationModel)
                {
                    failures.Add(result.ErrorMessage ?? "CreateOperation failed");
                    return;
                }

                Interlocked.Increment(ref created);
            }
            finally
            {
                Interlocked.Decrement(ref pendingCreates);
            }
        });

        var searchTasks = Enumerable.Range(0, ParallelCreates).Select(async index =>
        {
            await using var session = _fixture.Open();
            var attempts = 0;
            while (attempts == 0 || (attempts < 8 && Volatile.Read(ref pendingCreates) > 0))
            {
                attempts++;
                try
                {
                    var page = await session.OperationQueries.Search(new OperationSearchModel
                    {
                        FacilityId = facilityIds[index],
                        IncludeDisabled = true,
                        PageSize = 20,
                        PageNumber = 1
                    });
                    if (page.Records == null)
                    {
                        failures.Add("search returned no records for " + facilityIds[index]);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(ex.Message);
                    return;
                }

                Interlocked.Increment(ref searches);
            }
        });

        await Task.WhenAll(createTasks.Concat(searchTasks)).WaitAsync(TimeSpan.FromMinutes(2));

        Assert.Empty(failures);
        Assert.Equal(ParallelCreates, created);
        Assert.True(searches >= ParallelCreates);

        await using var check = _fixture.Open();
        foreach (var facilityId in facilityIds)
        {
            var page = await check.OperationQueries.Search(new OperationSearchModel
            {
                FacilityId = facilityId,
                IncludeDisabled = true,
                PageSize = 10
            });
            Assert.Contains(page.Records, record => record.FacilityId == facilityId);
        }
    }

    [Fact]
    public async Task EmptyVendorList_RemovesExistingPresets()
    {
        await SeedResourceTypesAsync();
        await using var session = _fixture.Open();
        var vendorVersionId = Guid.NewGuid();
        var created = await session.Manager.CreateOperation(new CreateOperationModel
        {
            OperationType = "CopyProperty",
            OperationJson = CopyJson,
            ResourceTypes = ["Patient"],
            Name = "Vendor",
            Description = "vendor",
            VendorVersionIds = [vendorVersionId]
        });
        Assert.True(created.IsSuccess, created.ErrorMessage);
        var operation = Assert.IsType<OperationModel>(created.ObjectResult);

        await session.Manager.UpdateVendorPresetsForOperation(operation.Id, []);

        var remaining = await session.Context.VendorVersionOperationPresets.CountAsync(preset =>
            preset.OperationResourceType.OperationId == operation.Id);
        Assert.Equal(0, remaining);
    }

    /// <summary>
    /// Patient and Location start absent. Creating them inside the operation
    /// transaction hangs or fails: the first request inserts Patient and releases
    /// the process lock before commit, and the next request blocks on that key
    /// while holding the lock the first request needs for Location.
    /// </summary>
    [Fact]
    public async Task CreateOperations_WhenResourceTypesAreMissing_AllSucceedWithOneRowPerName()
    {
        var databaseName = "norm_missing_" + Guid.NewGuid().ToString("N");
        var connectionString = await _fixture.CreateEmptySnapshotDatabaseAsync(databaseName);
        const int parallelCreates = 8;
        try
        {
            var failures = new ConcurrentBag<string>();
            var created = 0;
            var tasks = Enumerable.Range(0, parallelCreates).Select(async index =>
            {
                try
                {
                    await using var session = new NormalizationSqlServerFixture.Session(connectionString);
                    var result = await session.Manager.CreateOperation(new CreateOperationModel
                    {
                        FacilityId = "missing-types-" + Guid.NewGuid().ToString("N"),
                        OperationType = "CopyProperty",
                        OperationJson = CopyJson,
                        ResourceTypes = ["Patient", "Location"],
                        Name = "Missing types " + index,
                        Description = "missing resource types",
                        VendorVersionIds = []
                    });

                    if (!result.IsSuccess || result.ObjectResult is not OperationModel)
                    {
                        failures.Add(result.ErrorMessage ?? "CreateOperation failed");
                        return;
                    }

                    Interlocked.Increment(ref created);
                }
                catch (Exception ex)
                {
                    failures.Add(ex.Message);
                }
            });

            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromMinutes(2));

            Assert.Empty(failures);
            Assert.Equal(parallelCreates, created);

            await using var check = new NormalizationSqlServerFixture.Session(connectionString);
            var names = await check.Context.ResourceTypes.Select(resource => resource.Name).ToListAsync();
            Assert.Equal(1, names.Count(name => name == "Patient"));
            Assert.Equal(1, names.Count(name => name == "Location"));
        }
        finally
        {
            await _fixture.DropDatabaseAsync(databaseName);
        }
    }

    private async Task SeedResourceTypesAsync()
    {
        await using var session = _fixture.Open();
        foreach (var name in new[] { "Patient", "Location" })
        {
            if (!await session.Context.ResourceTypes.AnyAsync(resource => resource.Name == name))
            {
                session.Context.ResourceTypes.Add(new LantanaGroup.Link.Normalization.Domain.Entities.ResourceType { Name = name });
            }
        }

        await session.Context.SaveChangesAsync();
    }
}
