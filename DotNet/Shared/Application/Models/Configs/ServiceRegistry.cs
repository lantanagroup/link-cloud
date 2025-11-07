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
        public string TerminologyServiceUrl { get; set; } = null!;

        public string? TerminologyServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.TerminologyServiceUrl))
                    return this.TerminologyServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? TenantServiceApiUrl
        {
            get
            {
                var url = TenantService.TenantServiceUrl;
                if (url != null && !url.EndsWith("/api"))
                    return url.TrimEnd('/') + "/api";

                return url;
            }
        }

        public string AccountServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.AccountServiceUrl))
                    return this.AccountServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string AuditServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.AuditServiceUrl))
                    return this.AuditServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string CensusServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.CensusServiceUrl))
                    return this.CensusServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string DataAcquisitionServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.DataAcquisitionServiceUrl))
                    return this.DataAcquisitionServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string MeasureServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.MeasureServiceUrl))
                    return this.MeasureServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string NormalizationServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.NormalizationServiceUrl))
                    return this.NormalizationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string NotificationServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.NotificationServiceUrl))
                    return this.NotificationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string QueryDispatchServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.QueryDispatchServiceUrl))
                    return this.QueryDispatchServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string ReportServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.ReportServiceUrl))
                    return this.ReportServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string SubmissionServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.SubmissionServiceUrl))
                    return this.SubmissionServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string ValidationServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.ValidationServiceUrl))
                    return this.ValidationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }
    }
}
