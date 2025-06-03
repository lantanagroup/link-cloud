using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Domain.Queries;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IVendorOperationPresetManager
    {
        Task<VendorOperationPresetModel> CreateVendorOperationPreset(CreateVendorOperationPresetModel model);
    }

    public class VendorOperationPresetManager : IVendorOperationPresetManager
    {

        private readonly IDatabase _database;
        private readonly IVendorQueries _vendorQueries;
        public VendorOperationPresetManager(IDatabase database, IVendorQueries vendorQueries)
        {
            _database = database;
            _vendorQueries = vendorQueries;
        }

        public async Task<VendorOperationPresetModel> CreateVendorOperationPreset(CreateVendorOperationPresetModel model)
        {
            var result = await _database.VendorOperationPresets.AddAsync(new Entities.VendorOperationPreset()
            {
                Vendor = model.Vendor,
                Description = model.Description,
                Versions = model.Versions,
                CreateDate = DateTime.UtcNow,
            });

            await _database.SaveChangesAsync();

            return await _vendorQueries.GetOperationPreset(result.Id);
        }
    }
}
