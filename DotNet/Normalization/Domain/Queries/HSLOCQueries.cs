using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TenantVendorVersionModel = LantanaGroup.Link.Shared.Application.Models.Tenant.VendorVersionModel;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IHSLOCQueries
    {
        Task<List<HSLOC>> GetAll(bool includeInactive = false);
    }

    public class HSLOCQueries : IHSLOCQueries
    {
        private readonly NormalizationDbContext _dbContext;

        public HSLOCQueries(NormalizationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<HSLOC>> GetAll(bool includeInactive = false)
        {
            var query = _dbContext.HSLOCS.AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(h => h.IsActive);
            }

            return await query.ToListAsync();
        }
    }
}
