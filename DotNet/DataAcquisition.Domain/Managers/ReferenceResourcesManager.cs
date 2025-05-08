using DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public interface IReferenceResourcesManager
{
    Task<ReferenceResources> AddAsync(ReferenceResources referenceResources, CancellationToken cancellationToken = default);
    Task<List<ReferenceResources>> GetReferenceResourcesForListOfIds(List<string> ids, string facilityId, CancellationToken cancellationToken = default);
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
        return await _database.ReferenceResourcesRepository.AddAsync(referenceResources, cancellationToken); 
    }

    public async Task<List<ReferenceResources>> GetReferenceResourcesForListOfIds(List<string> ids, string facilityId, CancellationToken cancellationToken = default)
    {
        List<ReferenceResources> referenceResources = new List<ReferenceResources>();
        foreach (var id in ids)
        {
            var result = await _database.ReferenceResourcesRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.ResourceId == id, cancellationToken);
            if (result != null)
            {
                referenceResources.Add(result);
            }
        }
        return referenceResources;
    }
}
