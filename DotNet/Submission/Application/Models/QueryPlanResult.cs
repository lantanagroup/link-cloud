namespace LantanaGroup.Link.Submission.Application.Models
{
    public class QueryPlanResult
    {
        public QueryPlan QueryPlan { get; set; }
    }

    public class QueryPlan
    {
        public string PlanName { get; set; }
        public string ReportType { get; set; }
        public string FacilityId { get; set; }
        public string EHRDescription { get; set; }
        public string LookBack { get; set; }
        public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
        public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }
    }

    public class InitialQueryResult : IQueryPlan
    {
        public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
    }
}
