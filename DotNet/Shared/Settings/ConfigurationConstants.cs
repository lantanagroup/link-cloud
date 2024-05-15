
namespace LantanaGroup.Link.Shared.Settings
{
    public class ConfigurationConstants
    {
        public static class AppSettings
        {
            public const string EnableSwagger = "EnableSwagger";
            public const string ServiceInformation = "ServiceInformation";
            public const string Telemetry = "Telemetry";
            public const string CORS = "CORS";
            public const string AutoMigrate = "AutoMigrate";
            public const string DatabaseProvider = "DatabaseProvider";
        }

        public static class DatabaseConnections
        {
            public const string SqlServer = "SqlServer";
            public const string DatabaseConnection = "DatabaseConnection";
        }
    }

    public static class LoggingIds
    {
        public const int GenerateItems = 1000;
        public const int ListItems = 1001;
        public const int GetItem = 1002;
        public const int InsertItem = 1003;
        public const int UpdateItem = 1004;
        public const int DeleteItem = 1005;
        public const int GetItemNotFound = 1006;
        public const int UpdateItemNotFound = 1007;
        public const int KafkaConsumer = 10008;
        public const int KafkaProducer = 10009;
        public const int HealthCheck = 10010;
    }
}
