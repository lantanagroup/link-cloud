using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public interface ICreatePatientEvent
    {
        Task<string> Execute(PatientEvent model, string? userId = null);
    }
}
