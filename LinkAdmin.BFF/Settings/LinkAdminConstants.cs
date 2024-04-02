﻿namespace LantanaGroup.Link.LinkAdmin.BFF.Settings
{
    public class LinkAdminConstants
    {
        public const string ServiceName = "LinkAdminBFF";

        public static class AppSettingsSectionNames
        {
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string ServiceInformation = "ServiceInformation";
            public const string IdentityProvider = "IdentityProviderConfig";
            public const string CORS = "CORS";
            public const string Telemetry = "TelemetryConfig";
            public const string Serilog = "Serilog";
            public const string Kafka = "KafkaConnection";
            public const string EnableSwagger = "EnableSwagger";
        }

        public static class AuthenticationSchemes
        {
            public const string Cookie = "link_cookie";
            public const string JwtBearerToken = "link_jwt_bearer";
            public const string Oauth2 = "link_oauth2";
            public const string OpenIdConnect = "link_openid_connect";
        }

        public static class LinkAdminLoggingIds
        {
            public const int RequestRecieved = 1000;
            public const int RequestRecievedWarning = 1001;
            public const int RequestRecievedException = 1002;
            public const int ApiRegistered = 1003;
            public const int KafkaProducerCreated  = 1004;
            public const int KafkaProducerException = 1005;
            public const int KafkaProducerPatientEvent = 1006;
            public const int KafkaProducerReportScheduled = 1007;
            public const int KafkaProducerDataAcquisitionRequested = 1008;
        }
    }
}
