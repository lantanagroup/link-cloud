using System.Text.Json.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RequestStatusModel
{
    [StringValue("Pending")]
    Pending,
    [StringValue("Processing")]
    Processing,
    [StringValue("Completed")]
    Completed,
    [StringValue("Failed")]
    Failed,
    [StringValue("Ready")]
    Ready,
    [StringValue("MaxRetriesReached")]
    MaxRetriesReached
}

public static class RequestStatusModelUtilities
{
    public static RequestStatusModel FromDomain(RequestStatus status)
    {
        return status switch
        {
            RequestStatus.Pending => RequestStatusModel.Pending,
            RequestStatus.Ready => RequestStatusModel.Ready,
            RequestStatus.Processing => RequestStatusModel.Processing,
            RequestStatus.Completed => RequestStatusModel.Completed,
            RequestStatus.Failed => RequestStatusModel.Failed,
            RequestStatus.MaxRetriesReached => RequestStatusModel.MaxRetriesReached,
            _ => throw new Exception($"Unknown RequestStatus: {status}"),
        };
    }

    public static RequestStatus ToDomain(RequestStatusModel status)
    {
        return status switch
        {
            RequestStatusModel.Pending => RequestStatus.Pending,
            RequestStatusModel.Ready => RequestStatus.Ready,
            RequestStatusModel.Processing => RequestStatus.Processing,
            RequestStatusModel.Completed => RequestStatus.Completed,
            RequestStatusModel.Failed => RequestStatus.Failed,
            RequestStatusModel.MaxRetriesReached => RequestStatus.MaxRetriesReached,
            _ => throw new Exception($"Unknown RequestStatusModel: {status}"),
        };
    }
}
