namespace LantanaGroup.Link.Census.Application.Interfaces
{
    public interface ICensusServiceMetrics
    {
        void IncrementPatientIdentifiedCounter(List<KeyValuePair<string, object?>> tags);
    }
}
