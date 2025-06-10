using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations
{
    /// <summary>
    /// Status of a copy operation.
    /// </summary>
    public enum OperationStatus
    {
        Failure, 
        Success
    }

    /// <summary>
    /// Result of a copy operation, including status, error message, and modified resource.
    /// </summary>
    public class OperationResult
    {
        public OperationStatus SuccessCode { get; }
        public string Result => SuccessCode.ToString();
        public string ErrorMessage { get; }
        public DomainResource Resource { get; }

        public OperationResult(OperationStatus successCode, string errorMessage, DomainResource resource)
        {
            SuccessCode = successCode;
            ErrorMessage = errorMessage ?? string.Empty;
            Resource = resource;
        }

        public static OperationResult Success(DomainResource resource) =>
            new OperationResult(OperationStatus.Success, string.Empty, resource);

        public static OperationResult Failure(string errorMessage, DomainResource resource = null) =>
            new OperationResult(OperationStatus.Failure, errorMessage, resource);
    }
}
