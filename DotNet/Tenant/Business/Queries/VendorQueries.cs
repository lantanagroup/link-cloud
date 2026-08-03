using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Tenant.Business.Queries
{
    public interface IVendorQueries
    {
        Task<VendorModel?> GetVendor(Guid Id);
        Task<List<VendorModel>> GetAll();
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
                })
                .ToListAsync();
        }
    }
}
