namespace LantanaGroup.Link.Submission.Application.Interfaces
{
    public interface ISubmissionServiceMetrics
    {
        void IncrementSubmissionCounter(List<KeyValuePair<string, object?>> tags);       
    }
}
