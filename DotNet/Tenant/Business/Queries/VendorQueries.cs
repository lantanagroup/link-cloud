using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Tenant.Business.Queries
{
    public interface IVendorQueries
    {
        Task<VendorModel?> GetVendor(Guid Id);
        Task<List<VendorModel>> GetAll();
        Task<VendorVersionModel?> GetVendorVersion(Guid Id);
        Task<List<VendorVersionModel>> GetAllVendorVersions();
        Task<List<VendorVersionModel>> GetVendorVersionsByVendorId(Guid vendorId);
    }

    public class VendorQueries : IVendorQueries
    {
        private readonly TenantDbContext _dbContext;
        public VendorQueries(TenantDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<VendorModel?> GetVendor(Guid Id)
        {
            return await _dbContext.Vendors
                .Where(v => v.Id == Id)
                .Select(v => new VendorModel
                {
                    Id = v.Id,
                    Name = v.Name,
                    Authentication = v.Authentication,
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<VendorModel>> GetAll()
        {
            return await _dbContext.Vendors
                .Select(v => new VendorModel
                {
                    Id = v.Id,
                    Name = v.Name,
                    Authentication = v.Authentication,
                })
                .ToListAsync();
        }

        public async Task<VendorVersionModel?> GetVendorVersion(Guid Id)
        {
            return await _dbContext.VendorVersions
                .Where(vv => vv.Id == Id)
                .Select(vv => new VendorVersionModel
                {
                    Id = vv.Id,
                    VendorId = vv.VendorId,
                    VendorName = vv.Vendor!.Name,
                    Version = vv.Version,
                    Authentication = vv.Vendor!.Authentication,
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<VendorVersionModel>> GetAllVendorVersions()
        {
            return await _dbContext.VendorVersions
                .Select(vv => new VendorVersionModel
                {
                    Id = vv.Id,
                    VendorId = vv.VendorId,
                    VendorName = vv.Vendor!.Name,
                    Version = vv.Version,
                    Authentication = vv.Vendor!.Authentication,
                })
                .ToListAsync();
        }

        public async Task<List<VendorVersionModel>> GetVendorVersionsByVendorId(Guid vendorId)
        {
            return await _dbContext.VendorVersions
                .Where(vv => vv.VendorId == vendorId)
                .Select(vv => new VendorVersionModel
                {
                    Id = vv.Id,
                    VendorId = vv.VendorId,
                    VendorName = vv.Vendor!.Name,
                    Version = vv.Version,
                    Authentication = vv.Vendor!.Authentication,
                })
                .ToListAsync();
        }
    }
}
