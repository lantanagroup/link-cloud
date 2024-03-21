namespace LantanaGroup.Link.LinkAdmin.BFF.Settings
{
    public class LinkAdminConstants
    {
        public static class AppSettingsSectionNames
        {
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string ServiceInformation = "ServiceInformation";
            public const string IdentityProvider = "IdentityProviderConfig";
            public const string CORS = "CORS";
            public const string Telemetry = "TelemetryConfig";
            public const string Serilog = "Logging:Serilog";
            public const string EnableSwagger = "EnableSwagger";
        }

        public static class LinkAdminLoggingIds
        {
            public const int RequestRecieved = 1000;
            public const int RequestRecievedWarning = 1001;
            public const int RequestRecievedException = 1002;
            public const int ApiRegistered = 1003;
        }
    }
}
