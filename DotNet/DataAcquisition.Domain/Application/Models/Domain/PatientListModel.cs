using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
public class PatientListModel
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ListType ListType { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TimeFrame TimeFrame { get; set; }
    public List<string> PatientIds { get; set; } = new List<string>();
}
