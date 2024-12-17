using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public interface ICreatePatientAcquired
    {
        Task<string> Execute(PatientAcquired model, string? userId = null);
    }
}
