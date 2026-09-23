using System.Data.Common;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Controllers;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class OperationSequenceCacheTests
{
    private const string CopyJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}";
    private const string UpdatedCopyJson = "{\"Name\": \"Updated Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}";

    [Fact]
    public async Task PostSequence_IsVisibleOnNextGet_ForFilteredAndUnfilteredReads()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var operationId = await harness.SeedOperationAsync(facilityId, "Keep", CopyJson);
        await harness.WarmAsync(facilityId);

        await harness.WriterManager.CreateOperationSequences(Sequence(facilityId, operationId));

        AssertOperationIds(await harness.GetAsync(harness.WriterController, facilityId, "Patient"), operationId);
        AssertOperationIds(await harness.GetAsync(harness.WriterController, facilityId), operationId);
        AssertOperationIds(await harness.GetAsync(harness.ReaderController, facilityId, "Patient"), operationId);
        AssertOperationIds(await harness.GetAsync(harness.ReaderController, facilityId), operationId);
    }

    [Fact]
    public async Task DeleteSequence_IsVisibleOnNextGet_ForFilteredAndUnfilteredReads()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var operationId = await harness.SeedAndSequenceAsync(facilityId, "Remove");
        await harness.WarmAsync(facilityId);

        var deleted = await harness.WriterManager.DeleteOperationSequence(new DeleteOperationSequencesModel
        {
            FacilityId = facilityId,
            ResourceType = "Patient"
        });

        Assert.True(deleted);
        Assert.Empty(await harness.GetAsync(harness.WriterController, facilityId, "Patient"));
        Assert.Empty(await harness.GetAsync(harness.WriterController, facilityId));
        Assert.Empty(await harness.GetAsync(harness.ReaderController, facilityId, "Patient"));
        Assert.Empty(await harness.GetAsync(harness.ReaderController, facilityId));
        Assert.Empty(await harness.WriterQueries.Search(Typed(facilityId)));
        Assert.Empty(await harness.ReaderQueries.Search(Typed(facilityId)));
    }

    [Fact]
    public async Task SequenceWrite_IsVisibleToTheListenerRead_OnEveryReplica()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var first = await harness.SeedAndSequenceAsync(facilityId, "First");
        var second = await harness.SeedOperationAsync(facilityId, "Second", CopyJson);
        await harness.WarmAsync(facilityId);

        await harness.WriterManager.CreateOperationSequences(Sequence(facilityId, second, first));

        AssertOperationIds(await harness.WriterQueries.Search(Typed(facilityId)), second, first);
        AssertOperationIds(await harness.ReaderQueries.Search(Typed(facilityId)), second, first);
        AssertOperationIds(await harness.WriterQueries.Search(All(facilityId)), second, first);
        AssertOperationIds(await harness.ReaderQueries.Search(All(facilityId)), second, first);
    }

    [Fact]
    public async Task OperationJsonUpdate_IsVisibleToTheListenerRead()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var operationId = await harness.SeedAndSequenceAsync(facilityId, "Copy");
        await harness.WarmAsync(facilityId);

        var updated = await harness.WriterManager.UpdateOperation(new UpdateOperationModel
        {
            Id = operationId,
            FacilityId = facilityId,
            Name = "Copy",
            Description = "Copy",
            OperationJson = UpdatedCopyJson,
            ResourceTypes = ["Patient"]
        });

        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        var writer = Assert.Single(await harness.WriterQueries.Search(Typed(facilityId)));
        var reader = Assert.Single(await harness.ReaderQueries.Search(Typed(facilityId)));
        Assert.Equal(UpdatedCopyJson, writer.OperationResourceType.Operation.OperationJson);
        Assert.Equal(UpdatedCopyJson, reader.OperationResourceType.Operation.OperationJson);
    }

    [Fact]
    public async Task DisableAndReenable_IsVisibleToTheListenerRead()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var operationId = await harness.SeedAndSequenceAsync(facilityId, "Copy");
        await harness.WarmAsync(facilityId);

        Assert.True((await harness.UpdateAsync(operationId, facilityId, CopyJson, isDisabled: true)).IsSuccess);
        Assert.True(Assert.Single(await harness.ReaderQueries.Search(Typed(facilityId))).OperationResourceType.Operation.IsDisabled);

        Assert.True((await harness.UpdateAsync(operationId, facilityId, CopyJson, isDisabled: false)).IsSuccess);
        Assert.False(Assert.Single(await harness.WriterQueries.Search(Typed(facilityId))).OperationResourceType.Operation.IsDisabled);
        Assert.False(Assert.Single(await harness.ReaderQueries.Search(Typed(facilityId))).OperationResourceType.Operation.IsDisabled);
    }

    [Fact]
    public async Task DeletedOperation_IsAbsentFromTheListenerRead()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var operationId = await harness.SeedAndSequenceAsync(facilityId, "Copy");
        await harness.WarmAsync(facilityId);

        var deleted = await harness.WriterManager.DeleteOperation(new DeleteOperationModel
        {
            FacilityId = facilityId,
            OperationId = operationId,
            ResourceType = "Patient"
        });

        Assert.True(deleted);
        Assert.Empty(await harness.WriterQueries.Search(Typed(facilityId)));
        Assert.Empty(await harness.ReaderQueries.Search(All(facilityId)));
        Assert.Empty(await harness.GetAsync(harness.ReaderController, facilityId, "Patient"));
    }

    [Fact]
    public async Task DeleteOperation_LocksPagedOperationsInAscendingIdOrder()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        for (var i = 0; i < 11; i++)
        {
            await harness.SeedAndSequenceAsync(facilityId, $"Op{i}");
        }

        var expected = (await harness.OperationQueries.Search(new OperationSearchModel
        {
            FacilityId = facilityId,
            IncludeDisabled = true,
            SortBy = "Id",
            SortOrder = SortOrder.Ascending,
            PageSize = 50,
            PageNumber = 1
        }, CancellationToken.None, hydrateVendors: false)).Records.Select(record => record.Id).ToList();

        harness.WriterCounter.OperationLockIds.Clear();
        Assert.True(await harness.WriterManager.DeleteOperation(new DeleteOperationModel { FacilityId = facilityId }));

        var locked = harness.WriterCounter.OperationLockIds;
        Assert.Equal(expected, locked.Distinct().ToList());
        for (var i = 1; i < locked.Count; i++)
        {
            Assert.True(expected.IndexOf(locked[i - 1]) <= expected.IndexOf(locked[i]));
        }

        Assert.Empty(await harness.WriterContext.Operations.Where(operation => operation.FacilityId == facilityId).ToListAsync());
    }

    [Fact]
    public async Task CreateOperationSequences_LocksExistingAndReplacementIdsInAscendingOrder()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var kept = await harness.SeedOperationAsync(facilityId, "Kept", CopyJson);
        var replaced = await harness.SeedOperationAsync(facilityId, "Replaced", CopyJson);
        await harness.WriterManager.CreateOperationSequences(Sequence(facilityId, kept, replaced));
        var incoming = await harness.SeedOperationAsync(facilityId, "Incoming", CopyJson);
        var expected = new[] { kept, replaced, incoming }.OrderBy(id => id).ToList();

        harness.WriterCounter.OperationLockIds.Clear();
        await harness.WriterManager.CreateOperationSequences(Sequence(facilityId, incoming));

        var locked = harness.WriterCounter.OperationLockIds;
        Assert.Equal(expected, locked.Distinct().ToList());
        for (var i = 1; i < locked.Count; i++)
        {
            Assert.True(expected.IndexOf(locked[i - 1]) <= expected.IndexOf(locked[i]));
        }

        AssertOperationIds(await harness.WriterQueries.Search(Typed(facilityId)), incoming);
    }

    [Fact]
    public async Task FacilityOperationCreate_AppendsToTheSequence_AndTheNextReadsSeeIt()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        var existing = await harness.SeedAndSequenceAsync(facilityId, "Existing");
        await harness.WarmAsync(facilityId);
        var controller = new OperationsController(
            harness.WriterManager,
            harness.OperationQueries,
            harness.WriterQueries,
            harness.Tenant.Object,
            null!, null!, null!, null!, null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var created = await controller.PostOperation(new PostOperationModel(
            ["Patient"],
            new CopyPropertyOperation("Created", "id", "meta.versionId", "created"),
            facilityId,
            null));

        var createdResult = Assert.IsType<CreatedResult>(created);
        var createdOperation = Assert.IsType<OperationModel>(createdResult.Value);
        AssertOperationIds(await harness.GetAsync(harness.WriterController, facilityId, "Patient"), existing, createdOperation.Id);
        AssertOperationIds(await harness.ReaderQueries.Search(All(facilityId)), existing, createdOperation.Id);
    }

    [Fact]
    public async Task VendorOperationUpdate_ReloadsTheFacilityThatSequencesIt_AndLeavesTheOtherFacilityCached()
    {
        using var harness = new Harness();
        var vendorVersionId = Guid.NewGuid();
        var sequenced = await harness.SeedOperationAsync("", "Vendor", CopyJson, vendorVersionId);
        var other = await harness.SeedAndSequenceAsync("facility-b", "Local");
        await harness.WriterManager.CreateOperationSequences(Sequence("facility-a", sequenced));
        await harness.WarmAsync("facility-a");
        await harness.WarmAsync("facility-b");

        var updated = await harness.WriterManager.UpdateOperation(new UpdateOperationModel
        {
            Id = sequenced,
            FacilityId = "",
            Name = "Vendor",
            Description = "Vendor",
            OperationJson = UpdatedCopyJson,
            ResourceTypes = ["Patient"],
            VendorVersionIds = [vendorVersionId]
        });

        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        var reloaded = Assert.Single(await harness.ReaderQueries.Search(Typed("facility-a")));
        Assert.NotNull(reloaded.OperationResourceType.Operation);
        Assert.Equal(UpdatedCopyJson, reloaded.OperationResourceType.Operation.OperationJson);
        var otherAfterAffectedReload = harness.ReaderCounter.SequenceCommands;
        var kept = Assert.Single(await harness.ReaderQueries.Search(Typed("facility-b")));
        Assert.Equal(otherAfterAffectedReload, harness.ReaderCounter.SequenceCommands);
        Assert.Equal(other, kept.OperationResourceType.OperationId);
    }

    [Fact]
    public async Task DeleteResource_DropsTheListenerCache_AndLeavesAnUnrelatedFacilityCached()
    {
        using var harness = new Harness();
        await harness.SeedAndSequenceAsync("facility-a", "Copy");
        var encounterOperationId = await harness.SeedOperationAsync("facility-b", "EncounterCopy", CopyJson, resourceName: "Encounter");
        await harness.WriterManager.CreateOperationSequences(Sequence("facility-b", "Encounter", encounterOperationId));
        await harness.WarmAsync("facility-a");
        await harness.WarmAsync("facility-b");

        await harness.Resources.DeleteResource("Patient");

        Assert.Empty(await harness.ReaderQueries.Search(Typed("facility-a")));
        Assert.Empty(await harness.ReaderQueries.Search(All("facility-a")));
        var otherBefore = harness.ReaderCounter.SequenceCommands;
        var kept = Assert.Single(await harness.ReaderQueries.Search(All("facility-b")));
        Assert.Equal(otherBefore, harness.ReaderCounter.SequenceCommands);
        Assert.Equal(encounterOperationId, kept.OperationResourceType.OperationId);
    }

    [Fact]
    public async Task DeleteResource_RemovesVendorPresets_AndDropsTheListenerCache()
    {
        using var harness = new Harness();
        var operationId = await harness.SeedOperationAsync("facility-a", "Copy", CopyJson, Guid.NewGuid());
        await harness.WriterManager.CreateOperationSequences(Sequence("facility-a", operationId));
        await harness.WarmAsync("facility-a");

        harness.WriterCounter.OperationLockIds.Clear();
        await harness.Resources.DeleteResource("Patient");

        Assert.Contains(operationId, harness.WriterCounter.OperationLockIds);
        Assert.Empty(harness.WriterContext.VendorVersionOperationPresets);
        Assert.Empty(await harness.ReaderQueries.Search(All("facility-a")));
    }

    [Fact]
    public async Task CreatePreset_RejectsAMappingDeletedWhileTheOperationLockWasHeld()
    {
        using var harness = new Harness();
        var operationId = await harness.SeedOperationAsync("facility-a", "Copy", CopyJson);
        var operationResourceTypeId = await harness.WriterContext.OperationResourceTypes
            .Where(map => map.OperationId == operationId)
            .Select(map => map.Id)
            .SingleAsync();
        await harness.WriterContext.OperationResourceTypes.FindAsync(operationResourceTypeId);

        var deleted = await harness.ReaderContext.OperationResourceTypes.SingleAsync(map => map.Id == operationResourceTypeId);
        harness.ReaderContext.OperationResourceTypes.Remove(deleted);
        await harness.ReaderContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Presets.Create(new CreateVendorVersionOperationPresetModel
        {
            VendorVersionId = Guid.NewGuid(),
            OperationResourceTypeId = operationResourceTypeId
        }));

        Assert.Contains("no longer exists", exception.Message, StringComparison.Ordinal);
        Assert.Empty(harness.WriterContext.VendorVersionOperationPresets.Local);
    }

    [Fact]
    public async Task LockFacilitySequenceWrites_SecondConnectionWaitsUntilTheFirstCommits()
    {
        var connectionString = $"Data Source=file:opseqlock{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False;Default Timeout=30";
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        await using (var setup = new NormalizationDbContext(new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlite(anchor).Options))
        {
            setup.Database.EnsureCreated();
            var resource = new LantanaGroup.Link.Normalization.Domain.Entities.ResourceType { Id = Guid.NewGuid(), Name = "Patient" };
            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                FacilityId = "facility-a",
                Name = "Copy",
                Description = "Copy",
                OperationType = "CopyProperty",
                OperationJson = CopyJson,
                CreateDate = DateTime.UtcNow
            };
            setup.ResourceTypes.Add(resource);
            setup.Operations.Add(operation);
            setup.OperationResourceTypes.Add(new OperationResourceType
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                ResourceTypeId = resource.Id
            });
            await setup.SaveChangesAsync();
        }

        await using var firstConnection = new SqliteConnection(connectionString);
        await using var secondConnection = new SqliteConnection(connectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();
        await using var first = new NormalizationDbContext(new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlite(firstConnection).Options);
        await using var second = new NormalizationDbContext(new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlite(secondConnection).Options);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = Mock.Of<IVendorVersionResolver>();
        var firstQueries = new OperationSequenceQueries(null!, first, cache, resolver);
        var secondQueries = new OperationSequenceQueries(null!, second, cache, resolver);
        var operationResourceTypeId = await first.OperationResourceTypes.Select(map => map.Id).SingleAsync();

        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await firstQueries.LockFacilitySequenceWritesAsync("facility-a");
        first.OperationSequences.Add(new OperationSequence
        {
            Id = Guid.NewGuid(),
            FacilityId = "facility-a",
            OperationResourceTypeId = operationResourceTypeId,
            Sequence = 1,
            CreateDate = DateTime.UtcNow
        });
        await first.SaveChangesAsync();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondLock = Task.Run(async () =>
        {
            started.TrySetResult();
            await using var secondTransaction = await second.Database.BeginTransactionAsync();
            await secondQueries.LockFacilitySequenceWritesAsync("facility-a");
            var sequences = await second.OperationSequences.CountAsync(sequence => sequence.FacilityId == "facility-a");
            await secondTransaction.CommitAsync();
            return sequences;
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(500);
        Assert.False(secondLock.IsCompleted);

        await firstTransaction.CommitAsync();
        var seen = await secondLock.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, seen);
        Assert.Equal(1, await first.OperationSequenceWriteLocks.CountAsync(lockRow => lockRow.FacilityId == "facility-a"));
    }

    [Fact]
    public async Task RemovingAResourceType_InvalidatesTheListenerCache()
    {
        using var harness = new Harness();
        var operationId = await harness.SeedAndSequenceAsync("facility-a", "Copy");
        await harness.WarmAsync("facility-a");

        harness.WriterCounter.OperationLockIds.Clear();
        await harness.WriterManager.UpdateOperationResourceTypesForOperation(operationId, new List<string> { "Encounter" });

        Assert.Contains(operationId, harness.WriterCounter.OperationLockIds);

        Assert.Empty(await harness.ReaderQueries.Search(Typed("facility-a")));
        Assert.Empty(await harness.WriterContext.OperationSequences.Where(sequence => sequence.FacilityId == "facility-a").ToListAsync());
    }

    [Fact]
    public async Task RemovingAResourceType_DeletesOnlyThatOperationResourceTypeSequences()
    {
        using var harness = new Harness();
        var operationId = await harness.SeedOperationAsync("facility-a", "Copy", CopyJson);
        var encounter = new LantanaGroup.Link.Normalization.Domain.Entities.ResourceType { Id = Guid.NewGuid(), Name = "Encounter" };
        harness.WriterContext.ResourceTypes.Add(encounter);
        harness.WriterContext.OperationResourceTypes.Add(new OperationResourceType
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            ResourceTypeId = encounter.Id
        });
        await harness.WriterContext.SaveChangesAsync();
        await harness.WriterManager.CreateOperationSequences(Sequence("facility-a", operationId));
        await harness.WriterManager.CreateOperationSequences(Sequence("facility-a", "Encounter", operationId));
        var otherOperationId = await harness.SeedAndSequenceAsync("facility-b", "Other");
        await harness.WarmAsync("facility-b");

        var updated = await harness.WriterManager.UpdateOperation(new UpdateOperationModel
        {
            Id = operationId,
            FacilityId = "facility-a",
            Name = "Copy",
            Description = "Copy",
            OperationJson = CopyJson,
            ResourceTypes = ["Encounter"]
        });

        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        Assert.Equal(1, await harness.ReaderContext.OperationSequences.CountAsync(sequence => sequence.FacilityId == "facility-b"));
        var otherBeforeReload = harness.ReaderCounter.SequenceCommands;
        var kept = Assert.Single(await harness.ReaderQueries.Search(Typed("facility-b")));
        Assert.Equal(otherBeforeReload, harness.ReaderCounter.SequenceCommands);
        Assert.Equal(otherOperationId, kept.OperationResourceType.OperationId);
        Assert.Empty(await harness.ReaderQueries.Search(Typed("facility-a")));
        Assert.Equal(operationId, Assert.Single(await harness.ReaderQueries.Search(new OperationSequenceSearchModel
        {
            FacilityId = "facility-a",
            ResourceType = "Encounter"
        })).OperationResourceType.OperationId);
    }

    [Fact]
    public async Task UnchangedFacility_DoesNotQuerySequencesOnTheNextSearch()
    {
        using var harness = new Harness();
        var facilityId = "facility-a";
        await harness.SeedAndSequenceAsync(facilityId, "Copy");

        await harness.WriterQueries.Search(Typed(facilityId));
        var sequencesAfterFirst = harness.WriterCounter.SequenceCommands;
        var revisionsAfterFirst = harness.WriterCounter.RevisionCommands;

        await harness.WriterQueries.Search(Typed(facilityId));

        Assert.Equal(sequencesAfterFirst, harness.WriterCounter.SequenceCommands);
        Assert.True(harness.WriterCounter.RevisionCommands > revisionsAfterFirst);
    }

    [Fact]
    public async Task FacilityWrite_DoesNotReloadAnotherFacility()
    {
        using var harness = new Harness();
        var kept = await harness.SeedAndSequenceAsync("facility-b", "Kept");
        var changed = await harness.SeedAndSequenceAsync("facility-a", "Changed");
        var replacement = await harness.SeedOperationAsync("facility-a", "Replacement", CopyJson);
        await harness.WarmAsync("facility-a");
        await harness.WarmAsync("facility-b");
        var otherBefore = harness.ReaderCounter.SequenceCommands;

        await harness.WriterManager.CreateOperationSequences(Sequence("facility-a", replacement));

        await harness.ReaderQueries.Search(Typed("facility-b"));
        Assert.Equal(otherBefore, harness.ReaderCounter.SequenceCommands);
        Assert.Equal(kept, Assert.Single(await harness.ReaderQueries.Search(All("facility-b"))).OperationResourceType.OperationId);
        AssertOperationIds(await harness.ReaderQueries.Search(Typed("facility-a")), replacement);
        Assert.NotEqual(changed, replacement);
    }

    [Fact]
    public async Task GetOperationSequence_DoesNotReadTheListenerCache()
    {
        var queries = new Mock<IOperationSequenceQueries>();
        queries.Setup(query => query.Search(It.IsAny<OperationSequenceSearchModel>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var tenant = new Mock<ITenantApiService>();
        tenant.Setup(service => service.CheckFacilityExists("facility-a", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = new OperationSequenceController(Mock.Of<IOperationManager>(), queries.Object, tenant.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetOperationSequence("facility-a", "Patient");

        Assert.IsType<OkObjectResult>(result);
        queries.Verify(query => query.Search(
            It.Is<OperationSequenceSearchModel>(model => model.FacilityId == "facility-a" && model.ResourceType == "Patient"),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OperationSequenceSearchModel Typed(string facilityId) => new() { FacilityId = facilityId, ResourceType = "Patient" };

    private static OperationSequenceSearchModel All(string facilityId) => new() { FacilityId = facilityId };

    private static CreateOperationSequencesModel Sequence(string facilityId, params Guid[] operationIds)
    {
        return Sequence(facilityId, "Patient", operationIds);
    }

    private static CreateOperationSequencesModel Sequence(string facilityId, string resourceType, params Guid[] operationIds)
    {
        return new CreateOperationSequencesModel
        {
            FacilityId = facilityId,
            ResourceType = resourceType,
            OperationSequences = operationIds.Select((operationId, index) => new CreateOperationSequenceModel
            {
                OperationId = operationId,
                Sequence = index + 1
            }).ToList()
        };
    }

    private static void AssertOperationIds(IReadOnlyList<OperationSequenceModel> sequences, params Guid[] operationIds)
    {
        Assert.Equal(operationIds, sequences.OrderBy(sequence => sequence.Sequence).Select(sequence => sequence.OperationResourceType.OperationId).ToArray());
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MemoryCache _writerCache = new(new MemoryCacheOptions());
        private readonly MemoryCache _readerCache = new(new MemoryCacheOptions());

        public Harness()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            // OperationSequence inserts rely on SQL Server defaults. SQLite has neither function.
            _connection.CreateFunction("getutcdate", () => DateTime.UtcNow);
            _connection.CreateFunction("newid", () => Guid.NewGuid());
            WriterCounter = new CommandCounter();
            ReaderCounter = new CommandCounter();
            WriterContext = CreateContext(WriterCounter);
            ReaderContext = CreateContext(ReaderCounter);
            Resolver = new Mock<IVendorVersionResolver>();
            Resolver.Setup(resolver => resolver.ResolveAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<Guid> vendorVersionIds, CancellationToken _) =>
                    Task.FromResult<IReadOnlyDictionary<Guid, VendorVersionModel>>(vendorVersionIds.Distinct().ToDictionary(
                        vendorVersionId => vendorVersionId,
                        vendorVersionId => new VendorVersionModel { Id = vendorVersionId, VendorId = Guid.Empty, Version = "test" })));
            Tenant = new Mock<ITenantApiService>();
            Tenant.Setup(service => service.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var writerDatabase = DatabaseFor(WriterContext);
            var resourceQueries = new ResourceQueries(WriterContext);
            OperationQueries = new OperationQueries(writerDatabase, WriterContext, Resolver.Object);
            WriterQueries = new OperationSequenceQueries(writerDatabase, WriterContext, _writerCache, Resolver.Object);
            ReaderQueries = new OperationSequenceQueries(DatabaseFor(ReaderContext), ReaderContext, _readerCache, Resolver.Object);
            Resources = new ResourceManager(writerDatabase, resourceQueries, WriterQueries, NullLogger<ResourceManager>.Instance);
            WriterManager = new OperationManager(
                writerDatabase,
                OperationQueries,
                WriterQueries,
                resourceQueries,
                Resources,
                Resolver.Object,
                Mock.Of<IHSLOCQueries>());
            Presets = new VendorVersionOperationPresetManager(
                writerDatabase,
                WriterManager,
                new VendorVersionOperationPresetQueries(WriterContext, Resolver.Object),
                Resolver.Object,
                WriterQueries);
            WriterController = ControllerFor(WriterManager, WriterQueries);
            ReaderController = ControllerFor(WriterManager, ReaderQueries);
        }

        public CommandCounter WriterCounter { get; }
        public CommandCounter ReaderCounter { get; }
        public NormalizationDbContext WriterContext { get; }
        public NormalizationDbContext ReaderContext { get; }
        public Mock<IVendorVersionResolver> Resolver { get; }
        public Mock<ITenantApiService> Tenant { get; }
        public OperationQueries OperationQueries { get; }
        public OperationSequenceQueries WriterQueries { get; }
        public OperationSequenceQueries ReaderQueries { get; }
        public ResourceManager Resources { get; }
        public OperationManager WriterManager { get; }
        public VendorVersionOperationPresetManager Presets { get; }
        public OperationSequenceController WriterController { get; }
        public OperationSequenceController ReaderController { get; }

        public async Task<Guid> SeedOperationAsync(string facilityId, string name, string json, Guid? vendorVersionId = null, string resourceName = "Patient")
        {
            var resource = await WriterContext.ResourceTypes.SingleOrDefaultAsync(candidate => candidate.Name == resourceName);
            if (resource == null)
            {
                resource = new LantanaGroup.Link.Normalization.Domain.Entities.ResourceType { Id = Guid.NewGuid(), Name = resourceName };
                WriterContext.ResourceTypes.Add(resource);
            }

            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                Name = name,
                Description = name,
                OperationType = "CopyProperty",
                OperationJson = json,
                CreateDate = DateTime.UtcNow
            };
            var map = new OperationResourceType
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                ResourceTypeId = resource.Id
            };
            WriterContext.Operations.Add(operation);
            WriterContext.OperationResourceTypes.Add(map);
            if (vendorVersionId.HasValue)
            {
                WriterContext.VendorVersionOperationPresets.Add(new VendorVersionOperationPreset
                {
                    Id = Guid.NewGuid(),
                    VendorVersionId = vendorVersionId.Value,
                    OperationResourceTypeId = map.Id,
                    CreateDate = DateTime.UtcNow
                });
            }

            await WriterContext.SaveChangesAsync();
            return operation.Id;
        }

        public async Task<Guid> SeedAndSequenceAsync(string facilityId, string name)
        {
            var operationId = await SeedOperationAsync(facilityId, name, CopyJson);
            await WriterManager.CreateOperationSequences(Sequence(facilityId, operationId));
            return operationId;
        }

        public async Task WarmAsync(string facilityId)
        {
            await WriterQueries.Search(All(facilityId));
            await WriterQueries.Search(Typed(facilityId));
            await ReaderQueries.Search(All(facilityId));
            await ReaderQueries.Search(Typed(facilityId));
        }

        public Task<TaskResult> UpdateAsync(Guid operationId, string facilityId, string json, bool isDisabled)
        {
            return WriterManager.UpdateOperation(new UpdateOperationModel
            {
                Id = operationId,
                FacilityId = facilityId,
                Name = "Copy",
                Description = "Copy",
                OperationJson = json,
                ResourceTypes = ["Patient"],
                IsDisabled = isDisabled
            });
        }

        public Task<List<OperationSequenceModel>> GetAsync(OperationSequenceController controller, string facilityId, string? resourceType = null)
        {
            return ReadGetAsync(controller, facilityId, resourceType);
        }

        public void Dispose()
        {
            WriterContext.Dispose();
            ReaderContext.Dispose();
            _connection.Dispose();
            _writerCache.Dispose();
            _readerCache.Dispose();
        }

        private NormalizationDbContext CreateContext(DbCommandInterceptor interceptor)
        {
            var options = new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlite(_connection).AddInterceptors(interceptor);
            var context = new NormalizationDbContext(options.Options);
            context.Database.EnsureCreated();
            return context;
        }

        private static Database DatabaseFor(NormalizationDbContext context)
        {
            return new Database(
                context,
                new OperationRepository(context),
                new OperationSequenceRepository(context),
                new ResourceTypeRepository(context),
                new OperationResourceTypeRepository(context),
                new VendorVersionOperationPresetRepository(context));
        }

        private OperationSequenceController ControllerFor(IOperationManager manager, IOperationSequenceQueries queries)
        {
            return new OperationSequenceController(manager, queries, Tenant.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        private static async Task<List<OperationSequenceModel>> ReadGetAsync(OperationSequenceController controller, string facilityId, string? resourceType)
        {
            var result = await controller.GetOperationSequence(facilityId, resourceType);
            var ok = Assert.IsType<OkObjectResult>(result);
            return Assert.IsType<List<OperationSequenceModel>>(ok.Value);
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int SequenceCommands { get; private set; }
        public int RevisionCommands { get; private set; }
        public List<Guid> OperationLockIds { get; } = new();

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Count(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            CaptureOperationLock(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            CaptureOperationLock(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void CaptureOperationLock(DbCommand command)
        {
            if (!command.CommandText.Contains("UPDATE Operation", StringComparison.OrdinalIgnoreCase)
                || !command.CommandText.Contains("ModifyDate", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (DbParameter parameter in command.Parameters)
            {
                if (parameter.Value is Guid id)
                {
                    OperationLockIds.Add(id);
                    return;
                }

                if (parameter.Value is string text && Guid.TryParse(text, out var parsed))
                {
                    OperationLockIds.Add(parsed);
                    return;
                }
            }
        }

        private void Count(string commandText)
        {
            if (commandText.Contains("OperationSequenceCacheRevision", StringComparison.OrdinalIgnoreCase))
            {
                RevisionCommands++;
            }
            else if (commandText.Contains("OperationSequence", StringComparison.OrdinalIgnoreCase))
            {
                SequenceCommands++;
            }
        }
    }
}
