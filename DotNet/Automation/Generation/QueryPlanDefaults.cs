namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Platform-agnostic default query plan definitions and resource-type extraction.
/// This class owns the canonical list of resource types that the automation pipeline
/// acquires. It has no serialization dependency — consumers that need wire-format
/// representations (e.g., JObject for the DataAcquisition API) should use the
/// platform-specific <c>QueryPlanBuilder</c> in Automation.Link.
/// </summary>
public static class QueryPlanDefaults
{
    /// <summary>
    /// Returns the distinct set of FHIR resource types that the query plan acquires
    /// (initial + supplemental). When <paramref name="externalInput"/> is provided,
    /// types are extracted from the external plan; otherwise the built-in defaults are used.
    /// Does NOT include Patient (always implicitly present) or pipeline-derived types
    /// (MeasureReport / OperationOutcome).
    /// </summary>
    public static HashSet<string> GetAcquiredResourceTypes(QueryPlanInput? externalInput = null)
    {
        if (externalInput != null)
            return externalInput.GetAcquiredResourceTypes();

        return GetDefaultAsInput().GetAcquiredResourceTypes();
    }

    /// <summary>
    /// Returns only the resource types acquired via <b>Parameter</b> queries (direct,
    /// patient-scoped searches).  Reference-queried types are excluded because their
    /// results depend on the content of other resources and may legitimately return
    /// fewer results than what was generated.
    /// </summary>
    public static HashSet<string> GetParameterQueryResourceTypes(QueryPlanInput? externalInput = null)
    {
        var plan = externalInput ?? GetDefaultAsInput();
        return plan.GetParameterQueryResourceTypes();
    }

    /// <summary>
    /// Returns the built-in default query plan as a <see cref="QueryPlanInput"/>.
    /// This is the single source of truth for the default queries that the automation
    /// pipeline uses when no custom query plan is configured.
    /// </summary>
    public static QueryPlanInput GetDefaultAsInput()
    {
        return new QueryPlanInput
        {
            EhrDescription = "Epic",
            LookBack = "P0D",
            InitialQueries = BuildDefaultInitialQueries(),
            SupplementalQueries = BuildDefaultSupplementalQueries()
        };
    }

    // ----- Default initial queries -----

    private static List<QueryPlanQueryEntry> BuildDefaultInitialQueries() =>
    [
        ParameterQuery("Encounter",
            VariableParam("patient", 0),
            VariableParam("date", 1, "ge{0}"),
            VariableParam("date", 3, "le{0}")),
        ParameterQuery("MedicationRequest",
            VariableParam("patient", 0)),
        ReferenceQuery("Location", 1, 100),
        ReferenceQuery("Medication", 1, 100)
    ];

    // ----- Default supplemental queries -----

    private static List<QueryPlanQueryEntry> BuildDefaultSupplementalQueries() =>
    [
        ParameterQuery("Condition",
            VariableParam("patient", 0),
            ResourceIdsParam("encounter", "Encounter", "100")),
        ParameterQuery("Coverage",
            VariableParam("patient", 0)),
        ParameterQuery("DiagnosticReport",
            VariableParam("patient", 0),
            VariableParam("date", 1, "ge{0}"),
            VariableParam("date", 3, "le{0}")),
        ParameterQuery("Observation",
            VariableParam("patient", 0),
            VariableParam("date", 1, "ge{0}"),
            VariableParam("date", 3, "le{0}"),
            LiteralParam("category", "imaging,laboratory,social-history,vital-signs")),
        ParameterQuery("Procedure",
            VariableParam("patient", 0),
            VariableParam("date", 1, "ge{0}"),
            VariableParam("date", 3, "le{0}")),
        ParameterQuery("ServiceRequest",
            VariableParam("patient", 0),
            ResourceIdsParam("encounter", "Encounter", "100")),
        ReferenceQuery("Device", 1, 100),
        ReferenceQuery("Specimen", 1, 100)
    ];

    // ----- Entry builders -----

    private static QueryPlanQueryEntry ParameterQuery(string resourceType, params QueryPlanParameterEntry[] parameters) => new()
    {
        ResourceType = resourceType,
        QueryConfigType = "Parameter",
        Parameters = [.. parameters]
    };

    private static QueryPlanQueryEntry ReferenceQuery(string resourceType, int operationType, int paged) => new()
    {
        ResourceType = resourceType,
        QueryConfigType = "Reference",
        OperationType = operationType,
        Paged = paged
    };

    private static QueryPlanParameterEntry VariableParam(string name, int variable, string? format = null) => new()
    {
        ParameterType = "Variable",
        Name = name,
        Variable = variable,
        Format = format
    };

    private static QueryPlanParameterEntry ResourceIdsParam(string name, string resource, string paged) => new()
    {
        ParameterType = "ResourceIds",
        Name = name,
        Resource = resource,
        PagedValue = paged
    };

    private static QueryPlanParameterEntry LiteralParam(string name, string literal) => new()
    {
        ParameterType = "Literal",
        Name = name,
        Literal = literal
    };
}
