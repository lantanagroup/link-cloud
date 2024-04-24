using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class ReferenceResourcesRepository : BaseSqlConfigurationRepo<ReferenceResources>, IReferenceResourcesRepository 
{
    private readonly ILogger<ReferenceResourcesRepository> _logger;
    private readonly DataAcquisitionDbContext _dbContext;

    public ReferenceResourcesRepository(ILogger<ReferenceResourcesRepository> logger, DataAcquisitionDbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<ReferenceResources>> GetReferenceResourcesForListOfIds(List<string> ids, string facilityId, CancellationToken cancellationToken = default)
    {
        List<ReferenceResources> referenceResources = new List<ReferenceResources>();
        foreach (var id in ids)
        {
            var result = await _dbContext.ReferenceResources.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.ResourceId == id);
            if (result != null)
            {
                referenceResources.Add(result);
            }
        }
        return referenceResources;
    }

    public void Dispose() { }
}
