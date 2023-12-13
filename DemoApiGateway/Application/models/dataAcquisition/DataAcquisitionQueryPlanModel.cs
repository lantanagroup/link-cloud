using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition
{
    public class DataAcquisitionQueryPlanModel
    {    
        public string PlanName { get; set; }
        public string ReportType { get; set; }
        public string FacilityId { get; set; }
        public string EHRDescription { get; set; }
        public string LookBack { get; set; }
        public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
        public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }
        public DataAcquisitionQueryPlanModel() { }

    }

    [JsonDerivedType(typeof(ParameterQueryConfig), nameof(ParameterQueryConfig))]
    [JsonDerivedType(typeof(ReferenceQueryConfig), nameof(ReferenceQueryConfig))]
    public abstract class IQueryConfig
    {
    }

    public class ParameterQueryConfig : IQueryConfig
    {
        public string ResourceType { get; set; }
        public List<IParameter> Parameters { get; set; }
    }

    public class ReferenceQueryConfig : IQueryConfig
    {
        public string ReferenceName { get; set; }
        public string ReferenceValue { get; set; }
    }

    [JsonDerivedType(typeof(LiteralParameter), nameof(LiteralParameter))]
    [JsonDerivedType(typeof(ResourceIdsParameter), nameof(ResourceIdsParameter))]
    [JsonDerivedType(typeof(VariableParameter), nameof(VariableParameter))]
    public abstract class IParameter
    {
    }

    public class LiteralParameter : IParameter
    {
        public string Name { get; set; }
        public string Literal { get; set; }
    }

    public class ResourceIdsParameter : IParameter
    {
        public string Name { get; set; }
        public string Resource { get; set; }
        public string Paged { get; set; }
    }

    public class VariableParameter : IParameter
    {
        public string Name { get; set; }
        public Variable Variable { get; set; }
        public string? Format { get; set; }
    }

    public enum Variable
    {
        PatientId, LookbackStart, PeriodStart, PeriodEnd
    }

}
