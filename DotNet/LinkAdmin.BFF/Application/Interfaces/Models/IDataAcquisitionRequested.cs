using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models
{
    public interface IDataAcquisitionRequested
    {
        string Key { get; set; }
        string PatientId { get; set; }
        string QueryType { get; set; }
        List<ScheduledReport> ScheduledReports { get; set; }
    }
}
