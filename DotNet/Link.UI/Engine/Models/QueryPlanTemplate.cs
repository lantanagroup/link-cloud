namespace Automation.UI.Models;

/// <summary>
/// A reusable, named query plan configuration that controls which FHIR resource types
/// are acquired during data acquisition. Stored in MongoDB and presented in the UI.
///
/// The query plan is independent of the measure — it defines *what data to fetch*,
/// while the measure defines *what CQL to evaluate against that data*.
/// </summary>
public class QueryPlanTemplate
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User-facing name for this query plan template.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description / notes.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// When true, this template was seeded by the system and cannot be modified or deleted.
    /// It can still be cloned.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Whether this is the default query plan template for new scenarios.
    /// Only one template should be marked as default at a time.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>EHR description passed to the DataAcquisition service (e.g., "Epic").</summary>
    public string EhrDescription { get; set; } = "Epic";

    /// <summary>ISO-8601 duration for look-back period (e.g., "P0D").</summary>
    public string LookBack { get; set; } = "P0D";

    /// <summary>
    /// The individual query entries for initial data acquisition.
    /// Each entry defines a resource type, query config type, and parameters.
    /// </summary>
    public List<QueryEntry> InitialQueries { get; set; } = [];

    /// <summary>
    /// The individual query entries for supplemental data acquisition.
    /// </summary>
    public List<QueryEntry> SupplementalQueries { get; set; } = [];

    /// <summary>When the template was created or last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns the distinct set of FHIR resource types that this query plan will acquire.
    /// </summary>
    public HashSet<string> GetAcquiredResourceTypes()
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in InitialQueries)
        {
            if (!string.IsNullOrWhiteSpace(q.ResourceType))
                types.Add(q.ResourceType);
        }
        foreach (var q in SupplementalQueries)
        {
            if (!string.IsNullOrWhiteSpace(q.ResourceType))
                types.Add(q.ResourceType);
        }
        return types;
    }
}

/// <summary>
/// A single query entry within a query plan template.
/// Maps to the structure consumed by the DataAcquisition service.
/// </summary>
public class QueryEntry
{
    /// <summary>The FHIR resource type to query (e.g., "Encounter", "Condition").</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>"Parameter" or "Reference".</summary>
    public string QueryConfigType { get; set; } = "Parameter";

    /// <summary>
    /// For Reference queries: the operation type (e.g., 2).
    /// </summary>
    public int? OperationType { get; set; }

    /// <summary>
    /// For Reference queries: the page size.
    /// </summary>
    public int? Paged { get; set; }

    /// <summary>
    /// For Parameter queries: the query parameters.
    /// </summary>
    public List<QueryParameter> Parameters { get; set; } = [];
}

/// <summary>
/// A parameter for a Parameter-type query entry.
/// </summary>
public class QueryParameter
{
    /// <summary>"Variable", "ResourceIds", or "Literal".</summary>
    public string ParameterType { get; set; } = "Variable";

    /// <summary>Parameter name (e.g., "patient", "date", "category").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>For Variable parameters: the variable index.</summary>
    public int? Variable { get; set; }

    /// <summary>For Variable parameters: the format string (e.g., "ge{0}").</summary>
    public string? Format { get; set; }

    /// <summary>For Literal parameters: the literal value.</summary>
    public string? Literal { get; set; }

    /// <summary>For ResourceIds parameters: the resource type name.</summary>
    public string? Resource { get; set; }

    /// <summary>For ResourceIds parameters: the page size.</summary>
    public string? PagedValue { get; set; }
}
