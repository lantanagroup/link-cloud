namespace LantanaGroup.Link.Notification.Settings
{
    public class NotificationConstants
    {
        public const string ServiceName = "Notification Service";        

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string Kafka = "KafkaConnection";
            public const string Mongo = "MongoDB";
            public const string Smtp = "SmtpConnection";
            public const string Channels = "Channels";
            public const string IdentityProvider = "IdentityProviderConfig";
            public const string Telemetry = "TelemetryConfig";
        }

        public static class NotificationLoggingIds
        {
            public const int GenerateItems = 1000;
            public const int ListItems = 1001;
            public const int GetItem = 1002;
            public const int InsertItem = 1003;
            public const int UpdateItem = 1004;
            public const int DeleteItem = 1005;
            public const int GetItemNotFound = 1006;
            public const int UpdateItemNotFound = 1007;
            public const int KafkaConsumer = 2000;
            public const int KafkaProducer = 2001;
            public const int EmailChannel = 3000;
            public const int HealthCheck = 9000;            
        }

    }
}
