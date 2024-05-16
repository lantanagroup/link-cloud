
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
            public const string DataProtection = "DataProtection";
        }

        public static class DatabaseConnections
        {
            public const string SqlServer = "SqlServer";
            public const string DatabaseConnection = "DatabaseConnection";
        }

        public static class LinkDataProtectors
        {
            public const string LinkUser = "LinkUser";
            public const string LinkSigningKey = "LinkSigningKey";
        }
    }
}
