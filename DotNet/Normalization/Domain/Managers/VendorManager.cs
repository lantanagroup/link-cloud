using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IVendorManager
    {
        Task<VendorModel?> CreateVendor(string vendorName);
        Task<VendorVersionModel> CreateVendorVersion(CreateVendorVersionModel model);
        Task<VendorVersionOperationPresetModel> CreateVendorVersionOperationPreset(CreateVendorVersionOperationPresetModel model);
        Task DeleteVendor(string vendor);
        Task DeleteVendor(Guid vendorId);
        Task DeleteVendorVersionOperationPreset(Guid vendor, Guid vendorVersionPresetId);
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

        public async Task<VendorModel?> CreateVendor(string vendorName)
        {
            if(string.IsNullOrEmpty(vendorName))
            {
                throw new ArgumentNullException(nameof(vendorName));
            }

            var existingVendor = await _vendorQueries.GetVendor(vendorName);

            if(existingVendor != null)
            {
                return null;
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
            var result = await _database.VendorVersionOperationPresets.AddAsync(new VendorVersionOperationPreset()
            {
                VendorVersionId = model.VendorVersionId,
                OperationResourceTypeId = model.OperationResourceTypeId,
                CreateDate = DateTime.UtcNow,
            });

            await _database.SaveChangesAsync();

            return await _vendorQueries.GetVendorVersionOperationPreset(result.Id);
        }

        public async Task DeleteVendor(string vendor)
        {
            var foundVendor = await _database.Vendors.SingleOrDefaultAsync(v => v.Name == vendor);

            if(foundVendor == null)
            {
                throw new InvalidOperationException($"No Vendor by the name of '{vendor.Sanitize()}' found.");
            }

            await DeleteVendor(foundVendor.Id);
        }

        public async Task DeleteVendor(Guid vendorId)
        {
            var foundVendor = await _database.Vendors.SingleAsync(v => v.Id == vendorId);

            var presets = await _database.VendorVersionOperationPresets.FindAsync(vvp => vvp.VendorVersion.VendorId == foundVendor.Id);
            presets.ForEach(_database.VendorVersionOperationPresets.Remove);

            var versions = await _database.VendorVersions.FindAsync(vv => vv.VendorId == foundVendor.Id);
            versions.ForEach(_database.VendorVersions.Remove);

            _database.Vendors.Remove(foundVendor);

            await _database.SaveChangesAsync();
        }

        public async Task DeleteVendorVersionOperationPreset(Guid vendor, Guid vendorVersionPresetId)
        {

            var vendorPreset = await _database.VendorVersionOperationPresets.SingleOrDefaultAsync(vvop => vvop.VendorVersion.Vendor.Id == vendor && vvop.Id == vendorVersionPresetId);

            if(vendorPreset == null)
            {
                throw new InvalidOperationException($"No Vendor Operation Preset exists for the provided id: {vendorVersionPresetId}");
            }

            _database.VendorVersionOperationPresets.Remove(vendorPreset);

            await _database.SaveChangesAsync();
        }
    }
}
