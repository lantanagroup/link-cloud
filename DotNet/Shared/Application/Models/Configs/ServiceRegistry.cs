using System.Security.Policy;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ServiceRegistry
    {
        public static string ConfigSectionName = "ServiceRegistry";
        public string AccountServiceUrl { get; set; } = null!;
        private string? _publicAccountServiceUrl;
        public string? PublicAccountServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicAccountServiceUrl) ? AccountServiceUrl : _publicAccountServiceUrl;
            set => _publicAccountServiceUrl = value;
        }
        
        public string AuditServiceUrl { get; set; } = null!;
        private string? _publicAuditServiceUrl;
        public string? PublicAuditServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicAuditServiceUrl) ? AuditServiceUrl : _publicAuditServiceUrl;
            set => _publicAuditServiceUrl = value;
        }
        
        public string CensusServiceUrl { get; set; } = null!;
        private string? _publicCensusServiceUrl;
        public string? PublicCensusServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicCensusServiceUrl) ? CensusServiceUrl : _publicCensusServiceUrl;
            set => _publicCensusServiceUrl = value;
        }
        
        public string DataAcquisitionServiceUrl { get; set; } = null!;
        private string? _publicDataAcquisitionServiceUrl;
        public string? PublicDataAcquisitionServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicDataAcquisitionServiceUrl) ? DataAcquisitionServiceUrl : _publicDataAcquisitionServiceUrl;
            set => _publicDataAcquisitionServiceUrl = value;
        }
        
        public string MeasureServiceUrl { get; set; } = null!;
        private string? _publicMeasureServiceUrl;
        public string? PublicMeasureServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicMeasureServiceUrl) ? MeasureServiceUrl : _publicMeasureServiceUrl;
            set => _publicMeasureServiceUrl = value;
        }
        
        public string NormalizationServiceUrl { get; set; } = null!;
        private string? _publicNormalizationServiceUrl;
        public string? PublicNormalizationServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicNormalizationServiceUrl) ? NormalizationServiceUrl : _publicNormalizationServiceUrl;
            set => _publicNormalizationServiceUrl = value;
        }
        
        public string NotificationServiceUrl { get; set; } = null!;
        private string? _publicNotificationServiceUrl;
        public string? PublicNotificationServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicNotificationServiceUrl) ? NotificationServiceUrl : _publicNotificationServiceUrl;
            set => _publicNotificationServiceUrl = value;
        }
        
        public string QueryDispatchServiceUrl { get; set; } = null!;
        private string? _publicQueryDispatchServiceUrl;
        public string? PublicQueryDispatchServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicQueryDispatchServiceUrl) ? QueryDispatchServiceUrl : _publicQueryDispatchServiceUrl;
            set => _publicQueryDispatchServiceUrl = value;
        }
        
        public string ReportServiceUrl { get; set; } = null!;
        private string? _publicReportServiceUrl;
        public string? PublicReportServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicReportServiceUrl) ? ReportServiceUrl : _publicReportServiceUrl;
            set => _publicReportServiceUrl = value;
        }
        
        public string SubmissionServiceUrl { get; set; } = null!;
        private string? _publicSubmissionServiceUrl;
        public string? PublicSubmissionServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicSubmissionServiceUrl) ? SubmissionServiceUrl : _publicSubmissionServiceUrl;
            set => _publicSubmissionServiceUrl = value;
        }
        
        public string ValidationServiceUrl { get; set; } = null!;
        private string? _publicValidationServiceUrl;
        public string? PublicValidationServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicValidationServiceUrl) ? ValidationServiceUrl : _publicValidationServiceUrl;
            set => _publicValidationServiceUrl = value;
        }
        
        public TenantServiceRegistration TenantService { get; set; } = null!;
        
        public string TerminologyServiceUrl { get; set; } = null!;
        private string? _publicTerminologyServiceUrl;
        public string? PublicTerminologyServiceUrl 
        { 
            get => string.IsNullOrEmpty(_publicTerminologyServiceUrl) ? TerminologyServiceUrl : _publicTerminologyServiceUrl;
            set => _publicTerminologyServiceUrl = value;
        }

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
