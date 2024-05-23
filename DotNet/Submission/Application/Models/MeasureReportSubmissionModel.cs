﻿using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Converters;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Submission.Application.Models
{
    public class MeasureReportSubmissionModel
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PatientResources { get; set; }
        public string OtherResources { get; set; }
    }
}
