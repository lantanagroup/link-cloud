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
        Task<List<HSLOC>> GetAll(bool includeInactive = false, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<string, Guid>> GetActiveLookup(CancellationToken cancellationToken = default);
    }

    public class HSLOCQueries : IHSLOCQueries
    {
        private readonly NormalizationDbContext _dbContext;
        private readonly IHSLOCLookupCache _lookupCache;

        public HSLOCQueries(NormalizationDbContext dbContext, IHSLOCLookupCache lookupCache)
        {
            _dbContext = dbContext;
            _lookupCache = lookupCache;
        }

        public Task<IReadOnlyDictionary<string, Guid>> GetActiveLookup(CancellationToken cancellationToken = default) =>
            _lookupCache.GetActiveLookup(_dbContext, cancellationToken);

        public async Task<List<HSLOC>> GetAll(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var query = _dbContext.HSLOCS.AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(h => h.IsActive);
            }

            return await query.ToListAsync(cancellationToken);
        }
    }
}
