using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
public class QueryPlanPutModel : QueryPlanBaseModel
{
    [Required, DataMember]
    public required string? Id { get; set; }

    public QueryPlan ToDomain()
    {
        Validate();

        return new QueryPlan
        {
            Id = this.Id,
            PlanName = this.PlanName,
            Type = this.Type.Value,
            FacilityId = this.FacilityId,
            EHRDescription = this.EHRDescription,
            LookBack = this.LookBack,
            InitialQueries = this.InitialQueries,
            SupplementalQueries = this.SupplementalQueries,
        };
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(this.Id))
            throw new ArgumentNullException(nameof(this.Id));
        if (!Guid.TryParse(this.Id, out _))
            throw new ArgumentException("Id must be a valid GUID.", nameof(this.Id));
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
