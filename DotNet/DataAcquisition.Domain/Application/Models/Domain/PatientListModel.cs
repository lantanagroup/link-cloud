using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
public class PatientListModel
{
    public ListType ListType { get; set; }
    public TimeFrame TimeFrame { get; set; }
    public List<string> PatientIds { get; set; } = new List<string>();
}
