using System;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions
{
    public class OpOutcomeException : FhirOperationException
    {
        public OpOutcomeException(string message, FhirOperationException innerException) : base(message, innerException.Status, innerException.Outcome)
        {
        }
    }
}
