using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Queries
{
    public interface IGetAllPatientDispatchQuery
    {
        Task<List<PatientDispatchEntity>> Execute();
    }
}
