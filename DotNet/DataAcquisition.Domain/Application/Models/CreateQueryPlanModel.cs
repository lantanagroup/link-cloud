using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;

namespace DataAcquisition.Domain.Application.Models
{
    public class CreateQueryPlanModel
    {
        public string PlanName { get; set; }
        public string FacilityId { get; set; }
        public string EHRDescription { get; set; }
        public string LookBack { get; set; }
        public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
        public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }
        public Frequency Type { get; set; }
    }
}
