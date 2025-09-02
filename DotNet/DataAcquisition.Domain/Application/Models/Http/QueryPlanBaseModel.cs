using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
public class QueryPlanBaseModel
{
    [DataMember]
    public string? PlanName { get; set; }
    [Required, DataMember]
    public required Frequency? Type { get; set; }
    [Required, DataMember]
    public required string? FacilityId { get; set; }
    [DataMember]
    public string? EHRDescription { get; set; }
    [DataMember]
    public string? LookBack { get; set; }
    [DataMember, Required, MinDictionaryCount(1)]
    public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
    [DataMember, Required, MinDictionaryCount(1)]
    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }
    [IgnoreDataMember, JsonIgnore]
    public DateTime? CreateDate { get; set; }
    [IgnoreDataMember, JsonIgnore]
    public DateTime? ModifyDate { get; set; }
}
