using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IOperationQueries
    {
        Task<OperationModel> Get(Guid Id, string? facilityId = null);
        Task<List<OperationModel>> Search(OperationSearchModel model);
    }

    public class OperationQueries : IOperationQueries
    {
        private readonly IDatabase _database;
        private readonly NormalizationDbContext _dbContext;
        public OperationQueries(IDatabase database, NormalizationDbContext dbContext) 
        {
            _database = database;
            _dbContext = dbContext;
        }

        public async Task<OperationModel> Get(Guid Id, string? FacilityId = null)
        {
            return (await Search(new OperationSearchModel()
            {
                Id = Id,
                FacilityId = FacilityId,
            })).Single();
        }

        public async Task<List<OperationModel>> Search(OperationSearchModel model)
        {
            var query = from o in _dbContext.Operations
                        where o.FacilityId == model.FacilityId
                        select new OperationModel()
                        {
                            Id = o.Id,
                            FacilityId = o.FacilityId,
                            Description = o.Description,
                            IsDisabled = o.IsDisabled,
                            ModifyDate = o.ModifyDate,
                            OperationJson = o.OperationJson,
                            OperationType = o.OperationType,
                            CreateDate = o.CreateDate,                           
                        };

            if (model.Id.HasValue)
            {
                query = query.Where(q => q.Id == model.Id);
            }

            if (!model.IncludeDisabled)
            {
                query = query.Where(q => !q.IsDisabled);
            }

            if(model.OperationType.HasValue)
            {
                var opType = model.OperationType.ToString();
                query = query.Where(q => q.OperationType == opType);
            }

            return await query.ToListAsync();
        }
    }
}
