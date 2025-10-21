using Newtonsoft.Json.Linq;
using LantanaGroup.Automation.Generation;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Platform-specific wire-format builder that converts <see cref="QueryPlanInput"/>
/// into the <see cref="JObject"/> structure expected by the DataAcquisition service API.
///
/// For the canonical default plan definitions and resource-type extraction, see
/// <see cref="QueryPlanDefaults"/> in the Automation project.
/// </summary>
public static class QueryPlanBuilder
{
    /// <summary>
    /// Returns the distinct set of FHIR resource types that the query plan acquires.
    /// Delegates to <see cref="QueryPlanDefaults.GetAcquiredResourceTypes"/>.
    /// </summary>
    public static HashSet<string> GetAcquiredResourceTypes(QueryPlanInput? externalInput = null)
        => QueryPlanDefaults.GetAcquiredResourceTypes(externalInput);

    /// <summary>
    /// Returns only the resource types acquired via Parameter queries.
    /// Delegates to <see cref="QueryPlanDefaults.GetParameterQueryResourceTypes"/>.
    /// </summary>
    public static HashSet<string> GetParameterQueryResourceTypes(QueryPlanInput? externalInput = null)
        => QueryPlanDefaults.GetParameterQueryResourceTypes(externalInput);

    /// <summary>
    /// Exports the built-in default query plan as a <see cref="QueryPlanInput"/>.
    /// Delegates to <see cref="QueryPlanDefaults.GetDefaultAsInput"/>.
    /// </summary>
    public static QueryPlanInput GetDefaultAsInput()
        => QueryPlanDefaults.GetDefaultAsInput();

    /// <summary>
    /// Builds a query plan JObject suitable for the DataAcquisition service API.
    /// When <paramref name="externalInput"/> is provided, its queries override the defaults.
    /// </summary>
    public static JObject BuildQueryPlan(
        string facilityId,
        string? measureId,
        string ehrDescription,
        string type,
        QueryPlanInput? externalInput = null)
    {
        var plan = externalInput ?? QueryPlanDefaults.GetDefaultAsInput();

        return new JObject
        {
            ["PlanName"] = measureId,
            ["FacilityId"] = facilityId,
            ["EHRDescription"] = plan.EhrDescription ?? ehrDescription,
            ["LookBack"] = plan.LookBack ?? "P0D",
            ["Type"] = type,
            ["InitialQueries"] = ConvertEntriesToJObject(plan.InitialQueries),
            ["SupplementalQueries"] = ConvertEntriesToJObject(plan.SupplementalQueries)
        };
    }

    // ----- JObject conversion -----

    private static JObject ConvertEntriesToJObject(List<QueryPlanQueryEntry> entries)
    {
        var obj = new JObject();
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            obj[i.ToString()] = string.Equals(e.QueryConfigType, "Reference", StringComparison.OrdinalIgnoreCase)
                ? BuildReferenceQuery(e.ResourceType, e.OperationType ?? 1, e.Paged ?? 100)
                : BuildParameterQuery(e.ResourceType, e.Parameters.Select(ConvertParameter).ToArray());
        }
        return obj;
    }

    private static JObject ConvertParameter(QueryPlanParameterEntry p) => p.ParameterType switch
    {
        "ResourceIds" => ResourceIdsParam(p.Name, p.Resource ?? "", p.PagedValue ?? "100"),
        "Literal" => LiteralParam(p.Name, p.Literal ?? ""),
        _ => VariableParam(p.Name, p.Variable ?? 0, p.Format)
    };

    private static JObject BuildParameterQuery(string resourceType, params JObject[] parameters) => new()
    {
        ["QueryConfigType"] = "Parameter",
        ["ResourceType"] = resourceType,
        ["Parameters"] = new JArray(parameters)
    };

    private static JObject BuildReferenceQuery(string resourceType, int operationType, int paged) => new()
    {
        ["QueryConfigType"] = "Reference",
        ["ResourceType"] = resourceType,
        ["OperationType"] = operationType,
        ["Paged"] = paged
    };

    private static JObject VariableParam(string name, int variable, string? format = null) => new()
    {
        ["ParameterType"] = "Variable",
        ["Name"] = name,
        ["Variable"] = variable,
        ["Format"] = format
    };

    private static JObject ResourceIdsParam(string name, string resource, string paged) => new()
    {
        ["ParameterType"] = "ResourceIds",
        ["Name"] = name,
        ["Resource"] = resource,
        ["Paged"] = paged
    };

    private static JObject LiteralParam(string name, string literal) => new()
    {
        ["ParameterType"] = "Literal",
        ["Name"] = name,
        ["Literal"] = literal
    };
}
