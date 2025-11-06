using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class PatientListMessage
    {
        public string? ReportTrackingId { get; set; }
        public List<PatientListItem> PatientLists { get; set; } = new();
    }
}
