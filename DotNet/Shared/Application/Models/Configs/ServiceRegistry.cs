using System.Security.Policy;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ServiceRegistry
    {
        public static string ConfigSectionName = "ServiceRegistry";

        public string AccountServiceUrl { get; set; } = null!;
        public string? PublicAccountServiceUrl { get; set; }
        public string AuditServiceUrl { get; set; } = null!;
        public string? PublicAuditServiceUrl { get; set; }
        public string CensusServiceUrl { get; set; } = null!;
        public string? PublicCensusServiceUrl { get; set; }
        public string DataAcquisitionServiceUrl { get; set; } = null!;
        public string? PublicDataAcquisitionServiceUrl { get; set; }
        public string MeasureServiceUrl { get; set; } = null!;
        public string? PublicMeasureServiceUrl { get; set; }
        public string NormalizationServiceUrl { get; set; } = null!;
        public string? PublicNormalizationServiceUrl { get; set; }
        public string NotificationServiceUrl { get; set; } = null!;
        public string? PublicNotificationServiceUrl { get; set; }
        public string AdminBffServiceUrl { get; set; } = null!;
        public string? PublicAdminBffServiceUrl { get; set; }
        public string QueryDispatchServiceUrl { get; set; } = null!;
        public string? PublicQueryDispatchServiceUrl { get; set; }
        public string ReportServiceUrl { get; set; } = null!;
        public string? PublicReportServiceUrl { get; set; }
        public string SubmissionServiceUrl { get; set; } = null!;
        public string? PublicSubmissionServiceUrl { get; set; }
        public string ValidationServiceUrl { get; set; } = null!;
        public string? PublicValidationServiceUrl { get; set; }
        public TenantServiceRegistration TenantService { get; set; } = null!;
        public string TerminologyServiceUrl { get; set; } = null!;
        public string? PublicTerminologyServiceUrl { get; set; }

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

        public string AdminBffServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.AdminBffServiceUrl))
                {
                    if (this.AdminBffServiceUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                        return this.AdminBffServiceUrl.TrimEnd('/');

                    return this.AdminBffServiceUrl.TrimEnd('/') + "/api";
                }

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

        public string? PublicAccountServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicAccountServiceUrl))
                    return this.PublicAccountServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicAuditServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicAuditServiceUrl))
                    return this.PublicAuditServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicCensusServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicCensusServiceUrl))
                    return this.PublicCensusServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicDataAcquisitionServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicDataAcquisitionServiceUrl))
                    return this.PublicDataAcquisitionServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicMeasureServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicMeasureServiceUrl))
                    return this.PublicMeasureServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicNormalizationServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicNormalizationServiceUrl))
                    return this.PublicNormalizationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicNotificationServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicNotificationServiceUrl))
                    return this.PublicNotificationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicAdminBffServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicAdminBffServiceUrl))
                {
                    if (this.PublicAdminBffServiceUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                        return this.PublicAdminBffServiceUrl.TrimEnd('/');

                    return this.PublicAdminBffServiceUrl.TrimEnd('/') + "/api";
                }

                return null;
            }
        }

        public string? PublicQueryDispatchServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicQueryDispatchServiceUrl))
                    return this.PublicQueryDispatchServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicReportServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicReportServiceUrl))
                    return this.PublicReportServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicSubmissionServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicSubmissionServiceUrl))
                    return this.PublicSubmissionServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicValidationServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicValidationServiceUrl))
                    return this.PublicValidationServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }

        public string? PublicTerminologyServiceApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PublicTerminologyServiceUrl))
                    return this.PublicTerminologyServiceUrl.TrimEnd('/') + "/api";

                return null;
            }
        }
    }
}
