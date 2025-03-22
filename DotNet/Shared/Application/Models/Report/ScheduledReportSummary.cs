using System.Text.Json.Serialization;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.SerDes;

namespace LantanaGroup.Link.Shared.Application.Models.Report;

public class ScheduledReportSummary : ScheduledReportListSummary
{
    public List<PatientReportSummary> PatientReportSummaries { get; set; } = [];
    public List<ResourceSummary> SharedResources { get; set; } = [];
}

public class PatientReportSummary
{
    public required string Id { get; set; }
    public required string PatientId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public List<ResourceSummary> PatientResources { get; set; } = [];
}

public class ResourceSummary
{
    [JsonConverter(typeof(ResourceTypeJsonConverter))]
    public ResourceType ResourceType { get; set; }
    public string ResourceCategory { get; set; } = string.Empty; //TODO: Potentially move enum to shared project
    public int ResourceCount { get; set; }
}