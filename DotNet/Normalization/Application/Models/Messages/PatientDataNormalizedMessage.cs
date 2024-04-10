using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class PatientDataNormalizedMessage
{
    public string TenantId { get; set; }
    public string PatientId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string MeasureId { get; set; }
    public Bundle? Data { get; set; }
}
