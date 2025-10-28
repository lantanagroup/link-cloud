
namespace LantanaGroup.Link.Shared.Settings
{
    public static class ConfigurationConstants
    {
        public static class RunTimeConstants
        {
            public const string RetrySchedulerKeyedSingleton = "RetryScheduler";
        }
        public static class AppSettings
        {
            public const string EnableSwagger = "EnableSwagger";
            public const string ServiceInformation = "ServiceInformation";
            public const string Telemetry = "Telemetry";
            public const string CORS = "CORS";
            public const string AutoMigrate = "AutoMigrate";
            public const string DatabaseProvider = "DatabaseProvider";
            public const string SqlServerDatabaseProvider = "SqlServer";
            public const string DataProtection = "DataProtection";
            public const string LinkTokenService = "LinkTokenService";
            public const string SecretManagement = "SecretManagement";
            public const string Cache = "Cache";
            public const string DistributedLockSettings = "DistributedLockSettings";
            public const string RedisPassword = "Redis:Password";
        }

        public static class DatabaseConnections
        {
            public const string DatabaseConnection = "DatabaseConnection";
            public const string RedisConnection = "Redis";
        }

        public static class LinkDataProtectors
        {
            public const string LinkUser = "LinkUser";
            public const string LinkSigningKey = "LinkSigningKey";
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
