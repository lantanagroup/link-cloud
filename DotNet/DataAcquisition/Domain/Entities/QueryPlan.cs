using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[BsonCollection("queryPlan")]
public class QueryPlan : BaseEntity
{
    public string PlanName { get; set; }
    public string ReportType { get; set; }
    public string FacilityId { get; set; }
    public string EHRDescription { get; set; }
    public string LookBack { get; set; }
    public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}
