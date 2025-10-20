using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IReferenceResourcesManager
{
    Task<ReferenceResources> AddAsync(ReferenceResources referenceResources, CancellationToken cancellationToken = default);
    Task<ReferenceResources> UpdateAsync(ReferenceResources referenceResources, CancellationToken cancellationToken = default);
    Task<List<ReferenceResources>> GetReferenceResourcesForListOfIds(List<string> ids, string facilityId, CancellationToken cancellationToken = default);
    Task<ReferenceResources> GetByResourceIdAndFacilityId(string resourceId, string facilityId, CancellationToken cancellationToken = default);
    Task<List<ReferenceResources>> GetReferencesByFacilityAndResourceType(string facilityId, string resourceType, bool filterNullRefs, CancellationToken cancellationToken = default);
}

public class ReferenceResourcesManager : IReferenceResourcesManager
{
    private readonly ILogger<ReferenceResourcesManager> _logger;
    private readonly IDatabase _database;

    public ReferenceResourcesManager(ILogger<ReferenceResourcesManager> logger, IDatabase database)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<ReferenceResources> AddAsync(ReferenceResources referenceResources,
        CancellationToken cancellationToken = default)
    {
        var result = await _database.ReferenceResourcesRepository.AddAsync(referenceResources);
        await _database.ReferenceResourcesRepository.SaveChangesAsync();
        return result;
    }



    public async Task<ReferenceResources> GetByResourceIdAndFacilityId(string resourceId, string facilityId, CancellationToken cancellationToken = default)
    {
        return await _database.ReferenceResourcesRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.ResourceId == resourceId);
    }

    public async Task<List<ReferenceResources>> GetReferenceResourcesForListOfIds(List<string> ids, string facilityId, CancellationToken cancellationToken = default)
    {
        List<ReferenceResources> referenceResources = new List<ReferenceResources>();
        foreach (var id in ids)
        {
            var result = await _database.ReferenceResourcesRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.ResourceId == id);
            if (result != null)
            {
                referenceResources.Add(result);
            }
        }
        return referenceResources;
    }

    public async Task<List<ReferenceResources>> GetReferencesByFacilityAndResourceType(string facilityId, string resourceType, bool filterNullRefs, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("FacilityId cannot be null or empty", nameof(facilityId));
        
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("ResourceType cannot be null or empty", nameof(resourceType));
        
        return await _database.ReferenceResourcesRepository.FindAsync(x => x.FacilityId == facilityId && x.ResourceType == resourceType && (!filterNullRefs || x.ReferenceResource != null));
    }

    public async Task<ReferenceResources> UpdateAsync(ReferenceResources referenceResources, CancellationToken cancellationToken = default)
    {
        var existingReferenceResources = await _database.ReferenceResourcesRepository.FirstOrDefaultAsync(x => x.Id == referenceResources.Id);
        if (existingReferenceResources == null)
        {
            throw new KeyNotFoundException($"ReferenceResources with ID {referenceResources.Id} not found.");
        }
        existingReferenceResources.QueryPhase = referenceResources.QueryPhase;
        existingReferenceResources.ModifyDate = DateTime.UtcNow;
        existingReferenceResources.ResourceType = referenceResources.ResourceType;
        existingReferenceResources.ReferenceResource = referenceResources.ReferenceResource;
        
        await _database.ReferenceResourcesRepository.SaveChangesAsync();

        return existingReferenceResources;
    }
}
