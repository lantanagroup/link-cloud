using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

public class ResourceAcquired
{
    public bool AcquisitionComplete { get; set; } = false;
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public Resource Resource { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
    public ReportableEvent ReportableEvent { get; set; }

    // Serialize this object to a string for safe JobDataMap storage (FHIR-compliant)
    public string ToFhirJson()
    {
        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
        return JsonSerializer.Serialize(this, options);
    }

    // Deserialize from string (FHIR-compliant)
    public static ResourceAcquired FromFhirJson(string json)
    {
        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
        return JsonSerializer.Deserialize<ResourceAcquired>(json, options);
    }
}
