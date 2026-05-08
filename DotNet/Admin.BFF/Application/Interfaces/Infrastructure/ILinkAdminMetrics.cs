namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure
{
    public interface ILinkAdminMetrics
    {
        void IncrementUserLoginCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementFailedAuthenticationCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementTokenGeneratedCounter(List<KeyValuePair<string, object?>> tags);
        void IncrementTokenKeyRefreshCounter(List<KeyValuePair<string, object?>> tags);
    }
}
