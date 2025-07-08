using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[DataContract]
[Table("queryPlan")]
public class QueryPlan : BaseEntityExtended
{
    [DataMember]
    public string PlanName { get; set; } = string.Empty;
    [DataMember]
    public Frequency Type { get; set; }
    [DataMember]
    public string FacilityId { get; set; } = string.Empty;
    [DataMember]
    public string EHRDescription { get; set; } = string.Empty;
    [DataMember]
    public string LookBack { get; set; } = string.Empty;
    [DataMember]
    public Dictionary<string, IQueryConfig> InitialQueries { get; set; } = new();
    [DataMember]
    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; } = new();

    public QueryPlan() : base()
    {

    }


}
