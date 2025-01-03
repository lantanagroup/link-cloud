namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;

public class ServiceSpecAppenderConfig
{
    public static string ConfigSectionName = "ServiceSpecAppender";
    
    public string AccountServiceSpecUrl { get; set; } = null!;
    public string AccountServiceBffPrefix { get; set; } = null!;
    public string AccountServiceActualPrefix { get; set; } = null!;

    public string AuditServiceSpecUrl { get; set; } = null!;
    public string AuditServiceBffPrefix { get; set; } = null!;
    public string AuditServiceActualPrefix { get; set; } = null!;

    public string CensusServiceSpecUrl { get; set; } = null!;
    public string CensusServiceBffPrefix { get; set; } = null!;
    public string CensusServiceActualPrefix { get; set; } = null!;

    public string DataAcquisitionServiceSpecUrl { get; set; } = null!;
    public string DataAcquisitionServiceBffPrefix { get; set; } = null!;
    public string DataAcquisitionServiceActualPrefix { get; set; } = null!;

    public string MeasureServiceSpecUrl { get; set; } = null!;
    public string MeasureServiceBffPrefix { get; set; } = null!;
    public string MeasureServiceActualPrefix { get; set; } = null!;

    public string NormalizationServiceSpecUrl { get; set; } = null!;
    public string NormalizationServiceBffPrefix { get; set; } = null!;
    public string NormalizationServiceActualPrefix { get; set; } = null!;

    public string NotificationServiceSpecUrl { get; set; } = null!;
    public string NotificationServiceBffPrefix { get; set; } = null!;
    public string NotificationServiceActualPrefix { get; set; } = null!;

    public string QueryDispatchServiceSpecUrl { get; set; } = null!;
    public string QueryDispatchServiceBffPrefix { get; set; } = null!;
    public string QueryDispatchServiceActualPrefix { get; set; } = null!;

    public string ReportServiceSpecUrl { get; set; } = null!;
    public string ReportServiceBffPrefix { get; set; } = null!;
    public string ReportServiceActualPrefix { get; set; } = null!;

    public string SubmissionServiceSpecUrl { get; set; } = null!;
    public string SubmissionServiceBffPrefix { get; set; } = null!;
    public string SubmissionServiceActualPrefix { get; set; } = null!;

    public string TenantServiceSpecUrl { get; set; } = null!;
    public string TenantServiceBffPrefix { get; set; } = null!;
    public string TenantServiceActualPrefix { get; set; } = null!;

    public string ValidationServiceSpecUrl { get; set; } = null!;
    public string ValidationServiceBffPrefix { get; set; } = null!;
    public string ValidationServiceActualPrefix { get; set; } = null!;
}