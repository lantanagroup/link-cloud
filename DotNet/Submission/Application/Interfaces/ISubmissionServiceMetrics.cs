namespace LantanaGroup.Link.Submission.Application.Interfaces
{
    public interface ISubmissionServiceMetrics
    {
        List<KeyValuePair<string, object?>> BuildTags(string? correlationId, Guid reportScheduleId, string? patientId, string facilityId, string destinationType);
        void IncrementResourceCount(List<KeyValuePair<string, object?>> tags);
        void IncrementUploadCount(List<KeyValuePair<string, object?>> tags, string outcome);
        void RecordUploadDuration(double durationMilliseconds, List<KeyValuePair<string, object?>> tags);
        void RecordUploadSize(long sizeBytes, List<KeyValuePair<string, object?>> tags);
    }
}
