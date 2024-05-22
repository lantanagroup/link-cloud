﻿using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;

namespace LantanaGroup.Link.Report.Application.Models
{

    public class SubmissionReportValue
    {
        public List<string>? PatientIds { get; internal set; }
        public Organization Organization { get; internal set; }
        public List<string> Aggregates { get; internal set; }
        public string MeasureIds { get; set; }
    }
}
