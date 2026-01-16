using System;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions
{
    public class TooManyRequestsException : FhirOperationException
    {
        public TimeSpan? RetryAfter { get; }
        public TooManyRequestsException(string message, TimeSpan? retryAfter = null) : base(message, System.Net.HttpStatusCode.TooManyRequests)
        {
            RetryAfter = retryAfter;
        }
    }
}
