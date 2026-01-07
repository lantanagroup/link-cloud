namespace LantanaGroup.Link.Report.Domain.Enums
{
    public enum SubmissionStatus
    {
        PendingValidation,
        Submitting,
        Submitted,
        NotEligable //TODO: Implement setting this flag for invalid patients when we decide to not submit every measure report
    }
}
