using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration.CreatePatientEvent
{
    public interface ICreatePatientEvent
    {
        Task<string> Execute(PatientEvent model);
    }
}
