using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class QueryPlanModel
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

    public static QueryPlanModel FromDomain(QueryPlan entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        return new QueryPlanModel
        {
            Id = entity.Id,
            PlanName = entity.PlanName,
            FacilityId = entity.FacilityId,
            EHRDescription = entity.EHRDescription,
            LookBack = entity.LookBack,
            InitialQueries = entity.InitialQueries,
            SupplementalQueries = entity.SupplementalQueries,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate,
            Type = entity.Type
        };
    }
}