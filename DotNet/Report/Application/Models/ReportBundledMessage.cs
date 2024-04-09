using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Report.Application.Models;

public class ReportBundledMessage
{
    public static readonly string TopicName = nameof(KafkaTopic.ReportBundled);

    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string? FacilityId { get; set; }
    public string? ReportBundleId { get; set; }

}
