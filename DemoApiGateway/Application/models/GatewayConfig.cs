namespace LantanaGroup.Link.DemoApiGateway.Application.models
{
    public class GatewayConfig
    {
        public string AuditServiceApiUrl { get; set; }
        public string NotificationServiceApiUrl { get; set; }
        public string TenantServiceApiUrl { get; set; }
        public string CensusServiceApiUrl { get; set; }
        public string DataAcquisitionServiceApiUrl { get; set; }
        public string NormalizationServiceApiUrl { get; set; }
        public List<string> KafkaBootstrapServers { get; set; }
    }
}
