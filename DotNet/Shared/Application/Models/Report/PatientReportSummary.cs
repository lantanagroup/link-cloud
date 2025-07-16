using LantanaGroup.Link.Shared.Application.SerDes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Models.Report
{
    public class PatientReportSummary { 
        public int total { get; set; }
        public List<PatientSummary> Patients { get; set; } = new();
    }

    public class PatientSummary
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
    }
}
