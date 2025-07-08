using System.Collections.Generic;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

public class EhrPatientList
{
    public List<string> ListIds { get; set; } = new();
    public List<string> MeasureIds { get; set; } = new();
}
