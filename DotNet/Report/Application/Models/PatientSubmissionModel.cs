using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json.Serialization;
using LantanaGroup.Link.Shared.Application.Converters;

namespace LantanaGroup.Link.Report.Entities
{
    public class PatientSubmissionModel : ReportEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        [JsonConverter(typeof(FhirResourceConverter<Bundle>))]
        public Bundle PatientResources { get; set; }
        [JsonConverter(typeof(FhirResourceConverter<Bundle>))]
        public Bundle OtherResources { get; set; }
    }
}
