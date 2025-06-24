using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

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
    Ready
}

public static class RequestStatusModelUtilities
{
    public static RequestStatusModel FromDomain(RequestStatus status)
    {
        return status switch
        {
            RequestStatus.Pending => RequestStatusModel.Pending,
            RequestStatus.Processing => RequestStatusModel.Processing,
            RequestStatus.Completed => RequestStatusModel.Completed,
            RequestStatus.Failed => RequestStatusModel.Failed,
            _ => throw new Exception($"Unknown RequestStatus: {status}"),
        };
    }

    public static RequestStatus ToDomain(RequestStatusModel status)
    {
        return status switch
        {
            RequestStatusModel.Pending => RequestStatus.Pending,
            RequestStatusModel.Processing => RequestStatus.Processing,
            RequestStatusModel.Completed => RequestStatus.Completed,
            RequestStatusModel.Failed => RequestStatus.Failed,
            _ => throw new Exception($"Unknown RequestStatusModel: {status}"),
        };
    }
}
