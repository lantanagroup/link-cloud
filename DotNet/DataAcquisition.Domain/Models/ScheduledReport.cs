using LantanaGroup.Link.Shared.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Models;
public class ScheduledReport
{
    public string[] ReportTypes { get; set; }
    public Frequency Frequency { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
}
