using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Settings;

public class ApiSettings
{
    public FhirListSettings? FhirListSettings { get; set; }
}

public class FhirListSettings
{
    public List<string>? ValidStatuses { get; set; }
    public List<string>? ValidTimeFrames { get; set; }
}
