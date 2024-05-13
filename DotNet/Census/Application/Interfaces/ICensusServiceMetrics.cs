namespace LantanaGroup.Link.Census.Application.Interfaces
{
    public interface ICensusServiceMetrics
    {
        void IncrementPatientAdmittedCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementPatientDischargedCounter(List<KeyValuePair<string, object?>> tags);
    }
}
