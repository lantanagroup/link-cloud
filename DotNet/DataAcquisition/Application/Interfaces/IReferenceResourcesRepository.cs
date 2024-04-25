using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IReferenceResourcesRepository : IPersistenceRepository<ReferenceResources>, IDisposable
{
    Task<List<ReferenceResources>> GetReferenceResourcesForListOfIds(List<string> ids, string facilityId, CancellationToken cancellationToken = default);
}
