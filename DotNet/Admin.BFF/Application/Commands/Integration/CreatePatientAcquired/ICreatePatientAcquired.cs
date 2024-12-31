using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public interface ICreatePatientAcquired
    {
          /// <summary>
       /// Executes the patient acquisition command
       /// </summary>
       /// <param name="model">The patient acquisition data</param>
      /// <param name="userId">Optional ID of the user initiating the operation</param>
      /// <param name="cancellationToken">Token to cancel the operation</param>
       /// <returns>The ID of the created event</returns>
        Task<string> Execute([Required] PatientAcquired model, string? userId = null, CancellationToken cancellationToken = default);
    }
}
