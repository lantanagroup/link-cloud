namespace LantanaGroup.Link.LinkAdmin.BFF.Settings
{
    public class LinkAdminConstants
    {
        public static class AppSettingsSectionNames
        {
            public const string ExternalConfigurationSource = "Link:AdminBFF:ExternalConfigurationSource";
            public const string ServiceInformation = "Link:AdminBFF:ServiceInformation";
            public const string IdentityProvider = "Link:AdminBFF:IdentityProviderConfig";
            public const string Telemetry = "Link:AdminBFF:TelemetryConfig";
            public const string Serilog = "Link:AdminBFF:Logging:Serilog";
            public const string EnableSwagger = "Link:AdminBFF:EnableSwagger";
        }

        public static class AuditLoggingIds
        {
            public const int RequestRecieved = 1000;
            public const int RequestRecievedWarning = 1001;
            public const int RequestRecievedException = 1002;
        }
    }
}
