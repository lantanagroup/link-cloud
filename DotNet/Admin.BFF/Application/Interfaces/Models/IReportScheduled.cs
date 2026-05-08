using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models
{
    public interface IReportScheduled
    {        
        string FacilityId { get; set; }        
        List<string> ReportTypes { get; set; }        
        DateTime? StartDate { get; set; }        
        Frequency Frequency { get; set; }
        string Delay { get; set; }
    }
}
