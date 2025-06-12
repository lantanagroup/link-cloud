using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Normalization.Domain
{
    public interface IDatabase
    {
        Task SaveChangesAsync();
        IEntityRepository<Operation> Operations { get; set; }
        IEntityRepository<OperationSequence> OperationSequences { get; set; }
        IEntityRepository<ResourceType> ResourceTypes { get; set; }
        IEntityRepository<OperationResourceType> OperationResourceTypes { get; set; }
        IEntityRepository<VendorOperationPreset> VendorOperationPresets { get; set; }
        IEntityRepository<VendorPresetOperationResourceType> VendorPresetOperationResourceTypes { get; set; }
    }

    public class Database : IDatabase
    {
        private readonly NormalizationDbContext _dbContext;
        public IEntityRepository<Operation> Operations { get; set; }
        public IEntityRepository<OperationSequence> OperationSequences { get; set; }
        public IEntityRepository<ResourceType> ResourceTypes { get; set; }
        public IEntityRepository<OperationResourceType> OperationResourceTypes { get; set; }
        public IEntityRepository<VendorOperationPreset> VendorOperationPresets { get; set; }
        public IEntityRepository<VendorPresetOperationResourceType> VendorPresetOperationResourceTypes { get; set; }

        public Database(NormalizationDbContext dbContext, 
            IEntityRepository<Operation> operations, 
            IEntityRepository<OperationSequence> operationSequences, 
            IEntityRepository<ResourceType> resourceTypes, 
            IEntityRepository<OperationResourceType> operationResourceTypeMaps,
            IEntityRepository<VendorOperationPreset> vendorOperationPresets,
            IEntityRepository<VendorPresetOperationResourceType> vendorPresetOperationResourceTypes)
        {
            _dbContext = dbContext; 
            Operations = operations;
            OperationSequences = operationSequences;
            ResourceTypes = resourceTypes;
            OperationResourceTypes = operationResourceTypeMaps;
            VendorOperationPresets = vendorOperationPresets;
            VendorPresetOperationResourceTypes = vendorPresetOperationResourceTypes;
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
