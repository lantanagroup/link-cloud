namespace LantanaGroup.Link.Shared.Application.Models
{
    public class RetryListenerSettings
    {
        public string ServiceName { get; private set; }
        public string[] Topics { get; private set; }

        public RetryListenerSettings(string serviceName, string[] topics)
        {
            ServiceName = serviceName;
            Topics = topics;
        }

    }

}
