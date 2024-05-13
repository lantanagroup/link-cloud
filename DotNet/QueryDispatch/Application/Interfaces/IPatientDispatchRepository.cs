using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IPatientDispatchRepository : IPersistenceRepository<PatientDispatchEntity>
    {
        Task<bool> Delete(string facilityId, string patientId);
        Task<List<PatientDispatchEntity>> GetAllAsync();
    }
}
