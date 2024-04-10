using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Report.Application.Models;

public class ReportFailedMessage
{
    public static readonly string TopicName = nameof(KafkaTopic.ReportFailed);

    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string? ReportId { get; set; }
    public string? Message { get; set; }

}
