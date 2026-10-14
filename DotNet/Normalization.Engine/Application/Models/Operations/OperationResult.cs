using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations
{
    /// <summary>
    /// Status of a copy operation.
    /// </summary>
    public enum OperationStatus
    {
        Failure,
        Success,
        NoAction
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

        /// <summary>
        /// Per-code-map counts for this resource. Populated only by <c>CodeMapOperationService</c>; null on
        /// every other operation type, and null on <see cref="OperationStatus.Failure"/> because an
        /// operation that threw produced no counts to report.
        /// </summary>
        public IReadOnlyList<CodeMappingOutcome>? CodeMapping { get; }

        public OperationResult(OperationStatus successCode, string errorMessage, DomainResource resource, IReadOnlyList<CodeMappingOutcome>? codeMapping = null)
        {
            SuccessCode = successCode;
            ErrorMessage = errorMessage ?? string.Empty;
            Resource = resource;
            CodeMapping = codeMapping;
        }

        public static OperationResult Success(DomainResource resource, IReadOnlyList<CodeMappingOutcome>? codeMapping = null) =>
            new OperationResult(OperationStatus.Success, string.Empty, resource, codeMapping);

        public static OperationResult Failure(string errorMessage, DomainResource resource = null) =>
            new OperationResult(OperationStatus.Failure, errorMessage, resource);

        // NoAction carries the counts as well as Success: a code map that matched its FHIRPath but found no
        // entry for any code returns NoAction, and that is precisely the fully-unmapped case a report needs
        // to surface. Treating it as "nothing to say" would hide the worst result behind the same silence
        // as an operation that never ran.
        public static OperationResult NoAction(string errorMessage, DomainResource resource = null, IReadOnlyList<CodeMappingOutcome>? codeMapping = null) =>
            new OperationResult(OperationStatus.NoAction, errorMessage, resource, codeMapping);
    }
}
