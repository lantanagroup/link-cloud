using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Converters;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Submission.Application.Models
{
    public class SubmitReportValue
    {
        public List<string>? PatientIds { get; set; }

        //[JsonConverter(typeof(FhirResourceConverter<Organization>))]
        public Organization? Organization { get; set; }
        public List<MeasureReport> Aggregates { get; set; } = [];
        public string MeasureIds { get; set; } = default!;
    }
}
