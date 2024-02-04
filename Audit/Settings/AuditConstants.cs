namespace LantanaGroup.Link.Audit.Settings
{
    public class AuditConstants
    {
        public static class AppSettingsSectionNames
        {
            public const string ExternalConfigurationSource = "Link:Audit:ExternalConfigurationSource";
            public const string ServiceInformation = "Link:Audit:ServiceInformation";
            public const string Kafka = "Link:Audit:KafkaConnection";
            public const string Mongo = "Link:Audit:MongoDB";
            public const string IdentityProvider = "Link:Audit:IdentityProviderConfig";
            public const string Telemetry = "Link:Audit:TelemetryConfig";
            public const string Serilog = "Link:Audit:Logging:Serilog";
            public const string EnableSwagger = "Link:Audit:EnableSwagger";
            public const string AllowReflection = "Link:Audit:AllowReflection";
        }

        public static class AuditErrorMessages 
        {
            public const string NullOrWhiteSpaceFacilityId = "No facility id was given and is required for Audit events.";
            public const string NullOrWhiteSpaceServiceName = "No service name was given and is required for Audit events.";
        }

        public static class AuditLoggingIds 
        {
            public const int GenerateItems = 1000;
            public const int ListItems = 1001;
            public const int GetItem = 1002;
            public const int InsertItem = 1003;
            public const int UpdateItem = 1004;
            public const int DeleteItem = 1005;      
            public const int GetItemNotFound = 1006;
            public const int UpdateItemNotFound = 1007;
            public const int EventConsumer = 2000;
            public const int EventProducer = 2001;
            public const int HealthCheck = 9000;
        }
    }
}
