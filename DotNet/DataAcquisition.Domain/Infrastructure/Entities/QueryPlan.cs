using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("queryPlan")]
public class QueryPlan
{
    public Guid Id { get; set; }

    public string PlanName { get; set; }

    public string FacilityId { get; set; }

    public string EHRDescription { get; set; }

    public string LookBack { get; set; }

    public Dictionary<string, IQueryConfig> InitialQueries { get; set; }

    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }

    public Frequency Type { get; set; }
}
