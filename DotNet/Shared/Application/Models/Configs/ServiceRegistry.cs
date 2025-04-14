using System.Security.Policy;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ServiceRegistry
    {
        public static string ConfigSectionName = "ServiceRegistry";

        public string AccountServiceUrl { get; set; } = null!;
        public string AuditServiceUrl { get; set; } = null!;
        public string CensusServiceUrl { get; set; } = null!;
        public string DataAcquisitionServiceUrl { get; set; } = null!;
        public string MeasureServiceUrl { get; set; } = null!;
        public string NormalizationServiceUrl { get; set; } = null!;
        public string NotificationServiceUrl { get; set; } = null!;
        public string QueryDispatchServiceUrl { get; set; } = null!;
        public string ReportServiceUrl { get; set; } = null!;
        public string SubmissionServiceUrl { get; set; } = null!;
        public string ValidationServiceUrl { get; set; } = null!;
        public TenantServiceRegistration TenantService { get; set; } = null!;

        public string AccountServiceApiUrl
        {
            get
            {
                if (this.AccountServiceUrl != null)
                    return this.AccountServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string AuditServiceApiUrl
        {
            get
            {
                if (this.AuditServiceUrl != null)
                    return this.AuditServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string CensusServiceApiUrl
        {
            get
            {
                if (this.CensusServiceUrl != null)
                    return this.CensusServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string DataAcquisitionServiceApiUrl
        {
            get
            {
                if (this.DataAcquisitionServiceUrl != null)
                    return this.DataAcquisitionServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string MeasureServiceApiUrl
        {
            get
            {
                if (this.MeasureServiceUrl != null)
                    return this.MeasureServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string NormalizationServiceApiUrl
        {
            get
            {
                if (this.NormalizationServiceUrl != null)
                    return this.NormalizationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string NotificationServiceApiUrl
        {
            get
            {
                if (this.NotificationServiceUrl != null)
                    return this.NotificationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string QueryDispatchServiceApiUrl
        {
            get
            {
                if (this.QueryDispatchServiceUrl != null)
                    return this.QueryDispatchServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string ReportServiceApiUrl
        {
            get
            {
                if (this.ReportServiceUrl != null)
                    return this.ReportServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string SubmissionServiceApiUrl
        {
            get
            {
                if (this.SubmissionServiceUrl != null)
                    return this.SubmissionServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }
    }
}
