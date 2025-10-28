using System.Text.Json.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AcquisitionPriorityModel
{
    [StringValue("Normal")]
    Normal,
    [StringValue("High")]
    High,
    [StringValue("Critical")]
    Critical
}

public static class AcquisitionPriorityModelUtilities
{
    public static AcquisitionPriorityModel FromDomain(AcquisitionPriority priority)
    {
        return priority switch
        {
            AcquisitionPriority.Normal => AcquisitionPriorityModel.Normal,
            AcquisitionPriority.Critical => AcquisitionPriorityModel.Critical,
            AcquisitionPriority.High => AcquisitionPriorityModel.High,
            _ => AcquisitionPriorityModel.Normal
        };
    }

    public static AcquisitionPriority ToDomain(AcquisitionPriorityModel priority)
    {
        return priority switch
        {
            AcquisitionPriorityModel.Normal => AcquisitionPriority.Normal,
            AcquisitionPriorityModel.Critical => AcquisitionPriority.Critical,
            AcquisitionPriorityModel.High => AcquisitionPriority.High,
            _ => AcquisitionPriority.Normal
        };
    }
}