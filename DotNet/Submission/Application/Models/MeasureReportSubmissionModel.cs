using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Converters;
using System.Text.Json.Serialization;
using Hl7.Fhir.Serialization;

namespace LantanaGroup.Link.Submission.Application.Models
{
    public class MeasureReportSubmissionModel
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        [JsonConverter(typeof(FhirJsonConverter<Bundle>))]
        public Bundle PatientResources { get; set; }
        [JsonConverter(typeof(FhirJsonConverter<Bundle>))]
        public Bundle OtherResources { get; set; }
    }
}
