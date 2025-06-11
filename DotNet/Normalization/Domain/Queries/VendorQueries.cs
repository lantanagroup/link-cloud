using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IVendorQueries
    {
        Task<VendorOperationPresetModel> GetOperationPreset(Guid Id);
        Task<List<VendorOperationPresetModel>> SearchOperationPreset(VendorOperationPresetSearchModel model);
    }

    public class VendorQueries : IVendorQueries
    {
        private readonly IDatabase _database;
        private readonly NormalizationDbContext _dbContext;
        public VendorQueries(IDatabase database, NormalizationDbContext dbContext) 
        {
            _database = database;
            _dbContext = dbContext;
        }

        public async Task<VendorOperationPresetModel> GetOperationPreset(Guid Id)
        {
            return (await SearchOperationPreset(new VendorOperationPresetSearchModel()
            {
                Id = Id,
            })).Single();
        }

        public async Task<List<VendorOperationPresetModel>> SearchOperationPreset(VendorOperationPresetSearchModel model)
        {
            var query = from o in _dbContext.VendorOperationPresets
                        select new VendorOperationPresetModel()
                        {
                            Id = o.Id,
                            Vendor = o.Vendor,
                            Description = o.Description,
                            Versions = o.Versions,
                            CreateDate = o.CreateDate,
                            ModifyDate = o.ModifyDate
                        };

            if (model.Id.HasValue)
            {
                query = query.Where(q => q.Id == model.Id);
            }

            if (!string.IsNullOrEmpty(model.Vendor))
            {
                query = query.Where(q => q.Vendor == model.Vendor);
            }

            return await query.ToListAsync();
        }
    }
}
