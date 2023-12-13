using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Tenant.Config
{
    public static class TenantConstants
    {

        public const string ServiceName = "Tenant Service";

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string Kafka = "Kafka";
            public const string Mongo = "MongoDB";
            public const string Telemetry = "TelemetryConfig";
            public const string TenantConfig = "TenantConfig";
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
