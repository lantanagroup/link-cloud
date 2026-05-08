namespace LantanaGroup.Link.Account.Application.Interfaces.Infrastructure
{
    public interface IAccountServiceMetrics
    {
        void IncrementAccountAddedCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementAccountActiviatedCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementAccountDeactivatedCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementAccountRestoredCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementAccountDeletedCounter(List<KeyValuePair<string, object?>> tags);
    }
}
