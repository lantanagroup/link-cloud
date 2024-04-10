using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Report.Application.Models;

public class ReportSubmittedMessage
{
    public static readonly string TopicName = nameof(KafkaTopic.ReportSubmitted);

    public string ReportBundleId { get; set; } = string.Empty;

}
