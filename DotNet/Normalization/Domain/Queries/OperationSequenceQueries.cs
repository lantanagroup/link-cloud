using AngleSharp;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IOperationSequenceQueries
    {
        Task<OperationSequenceModel> Get(string resourceType, string? facilityId);
        Task<List<OperationSequenceModel>> Search(OperationSequenceSearchModel model, bool useCache = true);
        void ClearCache(OperationSequenceSearchModel model);
    }

    public class OperationSequenceQueries : IOperationSequenceQueries
    {
        private readonly IDatabase _database;
        private readonly NormalizationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(300); //5 mins

        public OperationSequenceQueries(IDatabase database, NormalizationDbContext dbContext, IMemoryCache cache) 
        {
            _database = database;
            _dbContext = dbContext;
            _cache = cache;
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

        private static (string? FacilityId, string? ResourceType, Guid? ResourceTypeId) BuildCacheKey(OperationSequenceSearchModel model) => (model.FacilityId, model.ResourceType, model.ResourceTypeId);


        public async Task<List<OperationSequenceModel>> Search(OperationSequenceSearchModel model, bool useCache = true)
        {
            if (!useCache) 
            {
                return Query(model);
            }

            //Daniel - 4/2026: Temporarily adding queried configs into memory cache to reduce the amount of queries made to the database.
            var cacheKey = BuildCacheKey(model);
            var cacheResult = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheTtl;

                return Query(model);
            });

            return cacheResult;
        }

        public void ClearCache(OperationSequenceSearchModel model) {
            _cache.Remove(BuildCacheKey(model));
        }

        private List<OperationSequenceModel> Query(OperationSequenceSearchModel model) 
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
                            VendorVersion = new VendorVersionModel()
                            {
                                Id = vp.VendorVersion.Id,
                                VendorId = vp.VendorVersion.VendorId,
                                Version = vp.VendorVersion.Version,
                                Vendor = new VendorModel()
                                {
                                    Id = vp.VendorVersion.Vendor.Id,
                                    Name = vp.VendorVersion.Vendor.Name
                                }
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

            return query.ToList();
        }
    }
}