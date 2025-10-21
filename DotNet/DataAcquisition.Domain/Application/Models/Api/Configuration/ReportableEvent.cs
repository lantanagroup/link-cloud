using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportableEvent
{
    [StringValue("Discharge")]
    Discharge,
    [StringValue("EOM")]
    EOM,
    [StringValue("EOW")]
    EOW,
    [StringValue("EOD")]
    EOD,
    [StringValue("Adhoc")]
    Adhoc
}
