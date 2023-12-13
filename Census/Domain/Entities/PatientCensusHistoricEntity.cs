using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Census.Domain.Entities;

[BsonCollection("patientCensusHistoricEntity")]
public class PatientCensusHistoricEntity : BaseEntity
{
    public string FacilityId { get; set; }
    public DateTime CensusDateTime { get; set; }
    private string reportId;
    public string ReportId
    {
        get { return reportId; }
        set
        {
            reportId = $"{FacilityId}-{CensusDateTime}";
        }
    }
}
