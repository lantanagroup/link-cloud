using System;
using System.Net;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions
{
    public class OpOutcomeException : FhirOperationException
    {
        public HttpStatusCode? StatusCode { get; }

        public OpOutcomeException(string message, FhirOperationException innerException) : base(message, innerException.Status, innerException.Outcome)
        {
            StatusCode = innerException.Status;
            Data.Add("InnerException", innerException);
        }

        public new FhirOperationException? InnerException => Data.Contains("InnerException") ? Data["InnerException"] as FhirOperationException : null;

        public override string? StackTrace => InnerException is not null ? $"{base.StackTrace}\n---> Inner Exception: {InnerException.Message}\n{InnerException.StackTrace}" : base.StackTrace;
    }
}
