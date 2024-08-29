using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Report.Application.Models;

public class ReportRequestRejectedMessage
{
    public static readonly string TopicName = nameof(KafkaTopic.ReportRequestRejected);

    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string? TenantId { get; set; }
    public string? ReportType { get; set; }
    public string? MyProperty { get; set; }

}
