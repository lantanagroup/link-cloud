using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace LantanaGroup.Link.Normalization.Domain
{
    public interface IDatabase
    {
        Task SaveChangesAsync();
        IEntityRepository<Operation> Operations { get; set; }
        IEntityRepository<OperationSequence> OperationSequences { get; set; }
        IEntityRepository<ResourceType> ResourceTypes { get; set; }
        IEntityRepository<OperationResourceType> OperationResourceTypes { get; set; }
        IEntityRepository<Vendor> Vendors { get; set; }
        IEntityRepository<VendorVersion> VendorVersions { get; set; }
        IEntityRepository<VendorVersionOperationPreset> VendorVersionOperationPresets { get; set; }

        Task<IDbContextTransaction> BeginTransactionAsync();
        Task RollbackTransactionAsync();
        Task CommitTransactionAsync();
    }

    public class Database : IDatabase
    {
        private readonly NormalizationDbContext _dbContext;
        public IEntityRepository<Operation> Operations { get; set; }
        public IEntityRepository<OperationSequence> OperationSequences { get; set; }
        public IEntityRepository<ResourceType> ResourceTypes { get; set; }
        public IEntityRepository<OperationResourceType> OperationResourceTypes { get; set; }
        public IEntityRepository<Vendor> Vendors { get; set; }
        public IEntityRepository<VendorVersion> VendorVersions { get; set; }
        public IEntityRepository<VendorVersionOperationPreset> VendorVersionOperationPresets { get; set; }

        public Database(NormalizationDbContext dbContext, 
            IEntityRepository<Operation> operations, 
            IEntityRepository<OperationSequence> operationSequences, 
            IEntityRepository<ResourceType> resourceTypes, 
            IEntityRepository<OperationResourceType> operationResourceTypeMaps,
            IEntityRepository<Vendor> vendors,
            IEntityRepository<VendorVersion> vendorVersions,
            IEntityRepository<VendorVersionOperationPreset> vendorOperationPresets)
        {
            _dbContext = dbContext; 
            Operations = operations;
            OperationSequences = operationSequences;
            ResourceTypes = resourceTypes;
            OperationResourceTypes = operationResourceTypeMaps;
            Vendors = vendors;
            VendorVersions = vendorVersions;
            VendorVersionOperationPresets = vendorOperationPresets;
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _dbContext.Database.BeginTransactionAsync();
        }

        public async Task RollbackTransactionAsync()
        {
            await _dbContext.Database.RollbackTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            await _dbContext.Database.CommitTransactionAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
