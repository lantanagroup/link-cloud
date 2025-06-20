using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IVendorManager
    {
        Task<VendorModel> CreateVendor(string vendorName);
        Task<VendorVersionModel> CreateVendorVersion(CreateVendorVersionModel model);
        Task<VendorVersionOperationPresetModel> CreateVendorVersionOperationPreset(CreateVendorVersionOperationPresetModel model);
    }

    public class VendorManager : IVendorManager
    {

        private readonly IDatabase _database;
        private readonly IVendorQueries _vendorQueries;
        public VendorManager(IDatabase database, IVendorQueries vendorQueries)
        {
            _database = database;
            _vendorQueries = vendorQueries;
        }

        public async Task<VendorModel> CreateVendor(string vendorName)
        {
            if(string.IsNullOrEmpty(vendorName))
            {
                throw new ArgumentNullException(nameof(vendorName));
            }

            var vendor = await _database.Vendors.AddAsync(new Vendor()
            {
                Name = vendorName
            });

            await _database.SaveChangesAsync();

            return new VendorModel()
            {
                Id = vendor.Id,
                Name = vendor.Name
            };
        }

        public async Task<VendorVersionModel> CreateVendorVersion(CreateVendorVersionModel model)
        {
            var vendorVersion = await _database.VendorVersions.AddAsync(new VendorVersion()
            {
                VendorId = model.VendorId,
                Version = model.Version
            });

            await _database.SaveChangesAsync();

            return new VendorVersionModel()
            {
                Id = vendorVersion.Id,
                VendorId = vendorVersion.VendorId,
                Version = vendorVersion.Version
            };
        }

        public async Task<VendorVersionOperationPresetModel> CreateVendorVersionOperationPreset(CreateVendorVersionOperationPresetModel model)
        {
            var result = await _database.VendorVersionOperationPresets.AddAsync(new Entities.VendorVersionOperationPreset()
            {
                VendorVersionId = model.VendorVersionId,
                OperationResourceTypeId = model.OperationResourceTypeId,
                CreateDate = DateTime.UtcNow,
            });

            await _database.SaveChangesAsync();

            return await _vendorQueries.GetOperationPreset(result.Id);
        }
    }
}
