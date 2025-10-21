using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public interface ICreatePatientListAcquired
    {
        Task<string> Execute([Required] PatientListAcquired model, string? userId = null, CancellationToken cancellationToken = default);
    }
}
