using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class CernerPatientsAcquiredValue
    {
        public string PatientId { get; set; }
        public string EncounterId { get; set; }
        public string FinNumber { get; set; }
        public string MRN { get; set; }
        public string EncounterStatus { get; set; }
        public string EncounterType { get; set; }
        public DateTime AdmitDate { get; set; }
    }

}
