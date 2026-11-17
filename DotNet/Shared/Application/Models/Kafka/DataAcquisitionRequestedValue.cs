using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

[DataContract]
public class DataAcquisitionRequestedValue
{
    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FacilityId { get; set; }
    [DataMember]
    public string PatientId { get; set; } = string.Empty;
    [DataMember]
    public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
    [DataMember]
    public string QueryType { get; set; } = Models.QueryType.Initial.ToString();
    public string ReportableEvent { get; set; } = string.Empty;
}

