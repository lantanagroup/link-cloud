using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
public class QueryPlanApiModel
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

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(this.PlanName))
            throw new ArgumentNullException(nameof(this.PlanName), "PlanName cannot be null or empty.");
        if (this.Type is null)
            throw new ArgumentNullException(nameof(this.Type), "Type is required.");
        if (string.IsNullOrWhiteSpace(this.FacilityId))
            throw new ArgumentNullException(nameof(this.FacilityId), "FacilityId is required.");
        if (this.InitialQueries is null || !this.InitialQueries.Any())
            throw new ArgumentNullException(nameof(this.InitialQueries), "InitialQueries is required.");
        if (this.SupplementalQueries is null || !this.SupplementalQueries.Any())
            throw new ArgumentNullException(nameof(this.SupplementalQueries), "SupplementalQueries is required.");

        return true;
    }
}
