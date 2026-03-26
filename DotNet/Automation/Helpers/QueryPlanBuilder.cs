using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Automation.Helpers;

public static class QueryPlanBuilder
{
    public static JObject BuildQueryPlan(string facilityId, string? measureId, string ehrDescription, string type)
    {
        return new JObject
        {
            ["PlanName"] = measureId,
            ["FacilityId"] = facilityId,
            ["EHRDescription"] = ehrDescription,
            ["LookBack"] = "P0D",
            ["Type"] = type,
            ["InitialQueries"] = BuildInitialQueries(),
            ["SupplementalQueries"] = BuildSupplementalQueries()
        };
    }

    private static JObject BuildInitialQueries()
    {
        return new JObject
        {
            ["0"] = BuildParameterQuery("Encounter",
                VariableParam("patient", 0),
                VariableParam("date", 1, "ge{0}"),
                VariableParam("date", 3, "le{0}")),
            ["1"] = BuildParameterQuery("MedicationRequest",
                VariableParam("patient", 0)),
            ["2"] = BuildReferenceQuery("Location", 2, 100),
            ["3"] = BuildReferenceQuery("Medication", 2, 100)
        };
    }

    private static JObject BuildSupplementalQueries()
    {
        return new JObject
        {
            ["0"] = BuildParameterQuery("Condition",
                VariableParam("patient", 0),
                ResourceIdsParam("encounter", "Encounter", "100")),
            ["1"] = BuildParameterQuery("Coverage",
                VariableParam("patient", 0)),
            ["2"] = BuildParameterQuery("DiagnosticReport",
                VariableParam("patient", 0),
                VariableParam("date", 1, "ge{0}"),
                VariableParam("date", 3, "le{0}")),
            ["3"] = BuildParameterQuery("Observation",
                VariableParam("patient", 0),
                VariableParam("date", 1, "ge{0}"),
                VariableParam("date", 3, "le{0}"),
                LiteralParam("category", "imaging,laboratory,social-history,vital-signs")),
            ["4"] = BuildParameterQuery("Procedure",
                VariableParam("patient", 0),
                VariableParam("date", 1, "ge{0}"),
                VariableParam("date", 3, "le{0}")),
            ["5"] = BuildParameterQuery("ServiceRequest",
                VariableParam("patient", 0),
                ResourceIdsParam("encounter", "Encounter", "100")),
            ["6"] = BuildReferenceQuery("Device", 2, 100),
            ["7"] = BuildReferenceQuery("Specimen", 2, 100)
        };
    }

    private static JObject BuildParameterQuery(string resourceType, params JObject[] parameters)
    {
        return new JObject
        {
            ["QueryConfigType"] = "Parameter",
            ["ResourceType"] = resourceType,
            ["Parameters"] = new JArray(parameters)
        };
    }

    private static JObject BuildReferenceQuery(string resourceType, int operationType, int paged)
    {
        return new JObject
        {
            ["QueryConfigType"] = "Reference",
            ["ResourceType"] = resourceType,
            ["OperationType"] = operationType,
            ["Paged"] = paged
        };
    }

    private static JObject VariableParam(string name, int variable, string? format = null)
    {
        return new JObject
        {
            ["ParameterType"] = "Variable",
            ["Name"] = name,
            ["Variable"] = variable,
            ["Format"] = format
        };
    }

    private static JObject ResourceIdsParam(string name, string resource, string paged)
    {
        return new JObject
        {
            ["ParameterType"] = "ResourceIds",
            ["Name"] = name,
            ["Resource"] = resource,
            ["Paged"] = paged
        };
    }

    private static JObject LiteralParam(string name, string literal)
    {
        return new JObject
        {
            ["ParameterType"] = "Literal",
            ["Name"] = name,
            ["Literal"] = literal
        };
    }
}
