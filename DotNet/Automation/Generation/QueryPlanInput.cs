namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Lightweight input that carries externally-defined query plan queries (initial + supplemental).
/// This is the platform-agnostic representation of a query plan — it describes *what* to acquire
/// without any wire-format serialization concern.
/// </summary>
public sealed class QueryPlanInput
{
    /// <summary>Initial query entries — keyed by ordinal.</summary>
    public List<QueryPlanQueryEntry> InitialQueries { get; set; } = [];

    /// <summary>Supplemental query entries — keyed by ordinal.</summary>
    public List<QueryPlanQueryEntry> SupplementalQueries { get; set; } = [];

    /// <summary>EHR description override. When null, the caller-provided value is used.</summary>
    public string? EhrDescription { get; set; }

    /// <summary>Look-back period override. When null, "P0D" is used.</summary>
    public string? LookBack { get; set; }

    /// <summary>
    /// Returns the distinct set of FHIR resource types that this query plan will acquire
    /// (initial + supplemental). Does NOT include the Patient anchor (always implicitly
    /// present) or pipeline-derived types like MeasureReport / OperationOutcome.
    /// </summary>
    public HashSet<string> GetAcquiredResourceTypes()
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in InitialQueries)
            if (!string.IsNullOrWhiteSpace(q.ResourceType))
                types.Add(q.ResourceType);
        foreach (var q in SupplementalQueries)
            if (!string.IsNullOrWhiteSpace(q.ResourceType))
                types.Add(q.ResourceType);
        return types;
    }

    /// <summary>
    /// Returns only resource types that are acquired via <b>Parameter</b> queries —
    /// direct, patient-scoped searches whose results are deterministic and can be
    /// count-verified.  Reference queries are excluded because their results depend on
    /// the content of other acquired resources and may legitimately return fewer (or zero)
    /// results than what was generated.
    /// </summary>
    public HashSet<string> GetParameterQueryResourceTypes()
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in InitialQueries.Concat(SupplementalQueries))
        {
            if (!string.IsNullOrWhiteSpace(q.ResourceType) && q.IsParameterQuery)
                types.Add(q.ResourceType);
        }
        return types;
    }
}

/// <summary>
/// A single query within a <see cref="QueryPlanInput"/>.
/// </summary>
public sealed class QueryPlanQueryEntry
{
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>"Parameter" or "Reference".</summary>
    public string QueryConfigType { get; set; } = "Parameter";

    /// <summary>For Reference queries.</summary>
    public int? OperationType { get; set; }

    /// <summary>For Reference queries.</summary>
    public int? Paged { get; set; }

    /// <summary>For Parameter queries.</summary>
    public List<QueryPlanParameterEntry> Parameters { get; set; } = [];

    /// <summary>True when this is a direct, patient-scoped Parameter query (not a Reference query).</summary>
    public bool IsParameterQuery => string.Equals(QueryConfigType, "Parameter", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when this is a Reference query (results depend on other resources).</summary>
    public bool IsReferenceQuery => string.Equals(QueryConfigType, "Reference", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A parameter within a Parameter-type <see cref="QueryPlanQueryEntry"/>.
/// </summary>
public sealed class QueryPlanParameterEntry
{
    /// <summary>"Variable", "ResourceIds", or "Literal".</summary>
    public string ParameterType { get; set; } = "Variable";
    public string Name { get; set; } = string.Empty;
    public int? Variable { get; set; }
    public string? Format { get; set; }
    public string? Literal { get; set; }
    public string? Resource { get; set; }
    public string? PagedValue { get; set; }
}
