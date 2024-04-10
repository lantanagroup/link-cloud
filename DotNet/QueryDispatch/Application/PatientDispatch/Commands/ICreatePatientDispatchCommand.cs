using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.PatientDispatch.Commands
{
    public interface ICreatePatientDispatchCommand
    {
        Task<string> Execute(PatientDispatchEntity patientDispatch, QueryDispatchConfigurationEntity queryDispatchConfiguration);
    }
}
