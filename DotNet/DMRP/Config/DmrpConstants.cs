namespace LantanaGroup.Link.DMRP.Config
{
    public static class DmrpConstants
    {
        public const string ServiceName = "DMRP";

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string Serilog = "Serilog";
            public const string DatabaseProvider = "DatabaseProvider";
        }

        public static class DmrpLoggingIds
        {
            public const int HealthCheck = 10010;
        }
    }
}
