﻿using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Converters;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Report.Entities
{
    public class PatientSubmissionModel : BaseEntityExtended
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
