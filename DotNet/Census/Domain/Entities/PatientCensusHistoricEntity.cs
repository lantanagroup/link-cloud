using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Census.Domain.Entities;

[Table("PatientCensusHistory")]
public class PatientCensusHistoricEntity : BaseEntityExtended
{
    public string FacilityId { get; set; } = string.Empty;
    public DateTime CensusDateTime { get; set; }
    private string reportId = string.Empty;

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public string ReportId
    {
        get { return reportId; }
        set
        {
            reportId = $"{FacilityId}-{CensusDateTime}";
        }
    }
}
