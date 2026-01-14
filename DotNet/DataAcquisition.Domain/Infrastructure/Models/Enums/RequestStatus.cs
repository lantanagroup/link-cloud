using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
public enum RequestStatus
{
    [StringValue("Pending")]
    Pending,
    [StringValue("Ready")]
    Ready,
    [StringValue("Processing")]
    Processing,
    [StringValue("Completed")]
    Completed,
    [StringValue("Failed")]
    Failed,
    [StringValue("Max Retries Reached")]
    MaxRetriesReached,
    [StringValue("Skipped")]
    Skipped
}
