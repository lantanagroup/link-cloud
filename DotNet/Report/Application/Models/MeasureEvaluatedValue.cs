using System.Text.Json;
using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class MeasureEvaluatedValue
    {
        public string PatientId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public JsonElement Result { get; set; }
        public string MeasureId { get; set; } = string.Empty;

    }
}
