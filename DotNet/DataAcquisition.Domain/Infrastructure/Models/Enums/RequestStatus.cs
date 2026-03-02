using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
//max char length for enum values: 50
public enum RequestStatus
{
    [StringValue("Pending")]
    Pending,
    [StringValue("Ready")]
    Ready,
    [StringValue("Queued")]
    Queued,
    [StringValue("Processing")]
    Processing,
    [StringValue("Completed")]
    Completed,
    [StringValue("Failed")]
    Failed,
    [StringValue("Max Retries Reached")]
    MaxRetriesReached,
    [StringValue("Skipped")]
    Skipped,
    [StringValue("Inoperable")]
    Inoperable
}
