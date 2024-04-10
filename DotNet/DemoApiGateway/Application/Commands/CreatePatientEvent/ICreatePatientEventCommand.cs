using LantanaGroup.Link.DemoApiGateway.Application.models.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.Commands.CreatePatientEvent
{
    public interface ICreatePatientEventCommand
    {
        Task<string> Execute(PatientEvent model);
    }
}
