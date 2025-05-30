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
        IEntityRepository<OperationResourceType> OperationResourceTypeMaps { get; set; }  
    }

    public class Database : IDatabase
    {
        private readonly NormalizationDbContext _dbContext;
        public IEntityRepository<Operation> Operations { get; set; }
        public IEntityRepository<OperationSequence> OperationSequences { get; set; }
        public IEntityRepository<ResourceType> ResourceTypes { get; set; }
        public IEntityRepository<OperationResourceType> OperationResourceTypeMaps { get; set; }
        
        public Database(NormalizationDbContext dbContext, IEntityRepository<Operation> operations, IEntityRepository<OperationSequence> operationSequences, IEntityRepository<ResourceType> resourceTypes, IEntityRepository<OperationResourceType> operationResourceTypeMaps)
        {
            _dbContext = dbContext; 
            Operations = operations;
            OperationSequences = operationSequences;
            ResourceTypes = resourceTypes;
            OperationResourceTypeMaps = operationResourceTypeMaps;
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
