using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class PatientResourcesNormalized
{
    public string CorrelationId { get; set; }
    public string TenantId { get; set; }
    public string ReportPeriod { get; set; }
    public Bundle PatientDataBundle { get; set; }
}
