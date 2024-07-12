namespace LantanaGroup.Link.Shared.Application.Models
{
    public class RetryListenerSettings
    {
        public string _serviceName { get; private set; }
        public string[] _topics { get; private set; }

        public RetryListenerSettings(string serviceName, string[] topics)
        {
            _serviceName = serviceName;
            _topics = topics;
        }

    }

}
