using AngleSharp;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using TenantVendorVersionModel = LantanaGroup.Link.Shared.Application.Models.Tenant.VendorVersionModel;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IOperationSequenceQueries
    {
        Task<OperationSequenceModel> Get(string resourceType, string? facilityId);
        Task<List<OperationSequenceModel>> Search(OperationSequenceSearchModel model, bool useCache = true, CancellationToken cancellationToken = default);
        Task ClearCache(OperationSequenceSearchModel model, CancellationToken cancellationToken = default);
        Task InvalidateFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
        Task InvalidateFacilitiesAsync(IEnumerable<string> facilityIds, CancellationToken cancellationToken = default);
        Task LockOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
        Task LockFacilitySequenceWritesAsync(string facilityId, CancellationToken cancellationToken = default);
        Task LockResourceTypeAsync(string resourceName, CancellationToken cancellationToken = default);
        Task<List<string>> FacilitiesUsingResourceTypeAsync(string resourceName, CancellationToken cancellationToken = default);
        Task<List<Guid>> OperationsInFacilitySequencesAsync(string facilityId, string? resourceType, CancellationToken cancellationToken = default);
        Task<List<string>> FacilitiesReferencingOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
    }

    public class OperationSequenceQueries : IOperationSequenceQueries
    {
        private readonly IDatabase _database;
        private readonly NormalizationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly IVendorVersionResolver _vendorVersionResolver;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(300); //5 mins

        public OperationSequenceQueries(IDatabase database, NormalizationDbContext dbContext, IMemoryCache cache, IVendorVersionResolver vendorVersionResolver)
        {
            _database = database;
            _dbContext = dbContext;
            _cache = cache;
            _vendorVersionResolver = vendorVersionResolver;
        }

        public async Task<OperationSequenceModel> Get(string FacilityId, Guid id)
        {
            return (await Search(new OperationSequenceSearchModel()
            {
                ResourceTypeId = id,
                FacilityId = FacilityId,
            })).Single();
        }

        public async Task<OperationSequenceModel> Get(string FacilityId, string resourceType)
        {
            return (await Search(new OperationSequenceSearchModel()
            {
                ResourceType = resourceType,
                FacilityId = FacilityId,
            })).Single();
        }

        private static (string? FacilityId, string? ResourceType, Guid? ResourceTypeId, long Revision) BuildCacheKey(OperationSequenceSearchModel model, long revision) => (model.FacilityId, model.ResourceType, model.ResourceTypeId, revision);


        public async Task<List<OperationSequenceModel>> Search(OperationSequenceSearchModel model, bool useCache = true, CancellationToken cancellationToken = default)
        {
            if (!useCache) 
            {
                return await QueryAsync(model, cancellationToken);
            }

            // The revision lives in the database, so a write committed on any replica changes the key
            // this process computes. Unchanged facilities keep their entries.
            var revision = await _dbContext.OperationSequenceCacheRevisions.AsNoTracking()
                .Where(row => row.FacilityId == model.FacilityId)
                .Select(row => row.Revision)
                .FirstOrDefaultAsync(cancellationToken);

            var cacheKey = BuildCacheKey(model, revision);
            if (_cache.TryGetValue(cacheKey, out List<OperationSequenceModel>? cacheResult) && cacheResult != null)
            {
                return cacheResult;
            }

            var result = await QueryAsync(model, cancellationToken);
            _cache.Set(cacheKey, result, _cacheTtl);

            return result;
        }

        public Task ClearCache(OperationSequenceSearchModel model, CancellationToken cancellationToken = default)
        {
            return InvalidateFacilityAsync(model.FacilityId, cancellationToken);
        }

        public async Task InvalidateFacilityAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(facilityId))
            {
                return;
            }

            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var revision = await _dbContext.OperationSequenceCacheRevisions
                    .FirstOrDefaultAsync(row => row.FacilityId == facilityId, cancellationToken);
                if (revision == null)
                {
                    _dbContext.OperationSequenceCacheRevisions.Add(new OperationSequenceCacheRevision
                    {
                        FacilityId = facilityId,
                        Revision = 1
                    });
                }
                else
                {
                    revision.Revision++;
                }

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    foreach (var entry in _dbContext.ChangeTracker.Entries<OperationSequenceCacheRevision>().ToList())
                    {
                        if (entry.State == EntityState.Added)
                        {
                            entry.State = EntityState.Detached;
                        }
                        else
                        {
                            await entry.ReloadAsync(cancellationToken);
                        }
                    }
                }
            }
        }

        public async Task InvalidateFacilitiesAsync(IEnumerable<string> facilityIds, CancellationToken cancellationToken = default)
        {
            foreach (var facilityId in facilityIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
            {
                await InvalidateFacilityAsync(facilityId, cancellationToken);
            }
        }

        public async Task LockFacilitySequenceWritesAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            // Not a facility revision. Sequence create and delete lock this row before operation rows,
            // so a delete-all waits out a concurrent create that uses a different operation id.
            if (!_dbContext.Database.IsRelational() || string.IsNullOrEmpty(facilityId))
            {
                return;
            }

            if (await HoldSequenceWriteLockAsync(facilityId, cancellationToken))
            {
                return;
            }

            var transaction = _dbContext.Database.CurrentTransaction;
            var savepoint = "s" + Guid.NewGuid().ToString("N")[..31];
            if (transaction != null)
            {
                await transaction.CreateSavepointAsync(savepoint, cancellationToken);
            }

            try
            {
                _dbContext.OperationSequenceWriteLocks.Add(new OperationSequenceWriteLock
                {
                    FacilityId = facilityId
                });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The other writer inserted the lock row. Roll back to the savepoint so this
                // transaction can still commit, then wait on that row.
                if (transaction != null)
                {
                    await transaction.RollbackToSavepointAsync(savepoint, cancellationToken);
                }

                foreach (var entry in _dbContext.ChangeTracker.Entries<OperationSequenceWriteLock>().ToList())
                {
                    if (entry.Entity.FacilityId == facilityId && entry.State == EntityState.Added)
                    {
                        entry.State = EntityState.Detached;
                    }
                }
            }

            if (!await HoldSequenceWriteLockAsync(facilityId, cancellationToken))
            {
                throw new InvalidOperationException("Could not acquire the operation sequence write lock.");
            }
        }

        private async Task<bool> HoldSequenceWriteLockAsync(string facilityId, CancellationToken cancellationToken)
        {
            var entityType = _dbContext.Model.FindEntityType(typeof(OperationSequenceWriteLock));
            var table = entityType?.GetTableName();
            if (string.IsNullOrEmpty(table))
            {
                return false;
            }

            var schema = entityType!.GetSchema();
            var target = string.IsNullOrEmpty(schema) ? table : schema + "." + table;
            var updated = await _dbContext.Database.ExecuteSqlRawAsync(
                $"UPDATE {target} SET FacilityId = FacilityId WHERE FacilityId = {{0}}",
                new object[] { facilityId },
                cancellationToken);
            return updated > 0;
        }

        public async Task LockResourceTypeAsync(string resourceName, CancellationToken cancellationToken = default)
        {
            // Sequence creates take this row before they insert, and resource deletes take it before
            // they look up facilities, so a delete cannot miss a facility a concurrent create adds.
            if (!_dbContext.Database.IsRelational() || string.IsNullOrEmpty(resourceName))
            {
                return;
            }

            var entityType = _dbContext.Model.FindEntityType(typeof(ResourceType));
            var table = entityType?.GetTableName();
            if (string.IsNullOrEmpty(table))
            {
                return;
            }

            var schema = entityType!.GetSchema();
            var target = string.IsNullOrEmpty(schema) ? table : schema + "." + table;
            await _dbContext.Database.ExecuteSqlRawAsync(
                $"UPDATE {target} SET Name = Name WHERE Name = {{0}}",
                new object[] { resourceName },
                cancellationToken);
        }

        public Task<List<string>> FacilitiesUsingResourceTypeAsync(string resourceName, CancellationToken cancellationToken = default)
        {
            return _dbContext.OperationSequences.AsNoTracking()
                .Where(sequence => sequence.OperationResourceType.ResourceType.Name == resourceName)
                .Select(sequence => sequence.FacilityId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task LockOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            // InMemory has no row locks. On SQL Server and SQLite this update keeps an exclusive lock
            // on the operation until the caller's transaction commits, so a sequence write cannot land
            // between the facility snapshot and the revision bump.
            if (!_dbContext.Database.IsRelational())
            {
                return;
            }

            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Operation SET ModifyDate = ModifyDate WHERE Id = {operationId}",
                cancellationToken);
        }

        public async Task<List<Guid>> OperationsInFacilitySequencesAsync(string facilityId, string? resourceType, CancellationToken cancellationToken = default)
        {
            var query = _dbContext.OperationSequences.AsNoTracking().Where(sequence => sequence.FacilityId == facilityId);
            if (!string.IsNullOrEmpty(resourceType))
            {
                query = query.Where(sequence => sequence.OperationResourceType.ResourceType.Name == resourceType);
            }

            return await query.Select(sequence => sequence.OperationResourceType.OperationId).Distinct().OrderBy(id => id).ToListAsync(cancellationToken);
        }

        public Task<List<string>> FacilitiesReferencingOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.OperationSequences.AsNoTracking()
                .Where(sequence => sequence.OperationResourceType.OperationId == operationId)
                .Select(sequence => sequence.FacilityId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        private async Task<List<OperationSequenceModel>> QueryAsync(OperationSequenceSearchModel model, CancellationToken cancellationToken)
        {
            IQueryable<OperationSequenceModel> query =
                    from o in _dbContext.OperationSequences
                    where o.FacilityId == model.FacilityId
                    select new OperationSequenceModel()
                    {
                        Id = o.Id,
                        FacilityId = o.FacilityId,
                        Sequence = o.Sequence.HasValue ? o.Sequence.Value : default,
                        ModifyDate = o.ModifyDate,
                        CreateDate = o.CreateDate,
                        OperationResourceType = new OperationResourceTypeModel()
                        {
                            Id = o.OperationResourceTypeId,
                            OperationId = o.OperationResourceType.OperationId,
                            ResourceTypeId = o.OperationResourceType.ResourceTypeId,
                            Operation = new OperationModel()
                            {
                                Id = o.OperationResourceType.OperationId,
                                FacilityId = o.OperationResourceType.Operation.FacilityId,
                                Name = o.OperationResourceType.Operation.Name,
                                Description = o.OperationResourceType.Operation.Description,
                                IsDisabled = o.OperationResourceType.Operation.IsDisabled,
                                ModifyDate = o.OperationResourceType.Operation.ModifyDate,
                                OperationJson = o.OperationResourceType.Operation.OperationJson,
                                OperationType = o.OperationResourceType.Operation.OperationType,
                                CreateDate = o.OperationResourceType.Operation.CreateDate,
                            },
                            Resource = new ResourceModel()
                            {
                                ResourceTypeId = o.OperationResourceType.ResourceType.Id,
                                ResourceName = o.OperationResourceType.ResourceType.Name
                            }
                        },
                        VendorPresets = o.OperationResourceType.VendorVersionOperationPresets.Select(vp => new VendorVersionOperationPresetModel()
                        {
                            Id = vp.Id,
                            VendorVersionId = vp.VendorVersionId,
                            OperationResourceTypeId = vp.OperationResourceTypeId,
                            OperationResourceType = new OperationResourceTypeModel()
                            {
                                Operation = new OperationModel()
                                {
                                    Id = vp.OperationResourceType.Operation.Id,
                                    Name = vp.OperationResourceType.Operation.Name,
                                    Description = vp.OperationResourceType.Operation.Description,
                                    OperationJson = vp.OperationResourceType.Operation.OperationJson,
                                    OperationType = vp.OperationResourceType.Operation.OperationType
                                },
                                Resource = new ResourceModel()
                                {
                                    ResourceName = vp.OperationResourceType.ResourceType.Name,
                                    ResourceTypeId = vp.OperationResourceType.ResourceType.Id
                                }
                            },
                            VendorVersion = new TenantVendorVersionModel()
                            {
                                Id = vp.VendorVersionId
                            },
                            CreateDate = vp.CreateDate,
                            ModifyDate = vp.ModifyDate
                        }).ToList()
                    };

            if (!string.IsNullOrEmpty(model.ResourceType))
            {
                query = query.Where(q => q.OperationResourceType.Resource.ResourceName == model.ResourceType);
            }

            if (model.ResourceTypeId.HasValue)
            {
                query = query.Where(q => q.OperationResourceType.Resource.ResourceTypeId == model.ResourceTypeId);
            }

            var records = await query.ToListAsync(cancellationToken);
            var presets = records.SelectMany(record => record.VendorPresets).ToList();
            if (presets.Count > 0)
            {
                var resolvedVendorVersions = await _vendorVersionResolver.ResolveAsync(presets.Select(preset => preset.VendorVersionId), cancellationToken);
                foreach (var preset in presets)
                {
                    preset.VendorVersion = resolvedVendorVersions[preset.VendorVersionId];
                }
            }

            return records;
        }
    }
}