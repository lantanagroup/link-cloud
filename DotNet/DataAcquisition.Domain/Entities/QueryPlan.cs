using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[DataContract]
[Table("queryPlan")]
public class QueryPlan : BaseEntityExtended
{
    [DataMember]
    public string PlanName { get; set; }
    [DataMember]
    public string ReportType { get; set; }
    [DataMember]
    public string FacilityId { get; set; }
    [DataMember]
    public string EHRDescription { get; set; }
    [DataMember]
    public string LookBack { get; set; }
    [DataMember]
    public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
    [DataMember]
    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }

    public QueryPlan() : base()
    {

    }
}