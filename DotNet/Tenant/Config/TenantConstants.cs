using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Tenant.Config
{
    public static class TenantConstants
    {

        public const string ServiceName = "Tenant";

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string KafkaConnection = "KafkaConnection";
            public const string Telemetry = "TelemetryConfig";
            public const string MeasureApiConfig = "MeasureServiceRegistry";
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string EnableSwagger = "EnableSwagger";
            public const string Serilog = "Logging:Serilog";
            public const string DatabaseProvider = "DatabaseProvider";
            public const string DatabaseConnectionString = "ConnectionStrings:DatabaseConnection";
        }

        public static class TenantLoggingIds
        {
            public const int HealthCheck = 10010;
        }

        public static class Scheduler
        {
            public const string JobName = "JobName";

            public const string JobGroup = "JobGroup";

            public const string JobTrigger = "JobTrigger";

            public const string Facility = "Facility";

            public const string ReportType = "ReportType";

            public const string StartDate = "StartDate";

            public const string EndDate =   "EndDate";
        }
        
    }
}
