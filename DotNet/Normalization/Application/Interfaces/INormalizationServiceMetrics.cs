namespace LantanaGroup.Link.Normalization.Application.Interfaces
{
    public interface INormalizationServiceMetrics
    {
        void IncrementResourceNormalizedCounter(List<KeyValuePair<string, object?>> tags);
    }
}
