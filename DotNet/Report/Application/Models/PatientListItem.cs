using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;

namespace LantanaGroup.Link.Report.Application.Models;

public class PatientListItem
{
    public ListType ListType { get; set; }
    public TimeFrame TimeFrame { get; set; }
    public List<string> PatientIds { get; set; } = new List<string>();
}

