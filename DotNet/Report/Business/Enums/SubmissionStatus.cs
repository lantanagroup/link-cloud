namespace LantanaGroup.Link.Report.Domain.Enums
{
    public enum SubmissionStatus
    {
        PendingValidation,
        Submitting,
        Submitted,
        FailedSubmission, //TODO: Consume submit payload error topic to update
        NotEligable //TODO: Implement setting this flag for invalid patients when we decide to not submit every measure report
    }
}