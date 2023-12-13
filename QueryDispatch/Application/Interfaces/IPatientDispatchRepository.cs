using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IPatientDispatchRepository : IBaseRepository<PatientDispatchEntity>
    {
        Task<bool> Delete(string facilityId, string patientId);
    }
}
