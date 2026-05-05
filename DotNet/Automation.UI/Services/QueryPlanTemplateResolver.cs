using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Resolves the query plan for a run from a stored template id, falling back to
/// the system default template and finally to the built-in <see cref="QueryPlanDefaults"/>.
/// Extracted from <see cref="AutomationRunManager"/> so the resolution logic is
/// testable in isolation and can be reused by other entry points.
/// </summary>
public sealed class QueryPlanTemplateResolver
{
    private readonly IQueryPlanTemplateStore _store;

    public QueryPlanTemplateResolver(IQueryPlanTemplateStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Resolves the query plan template for a run. Returns a resolution with
    /// <see cref="QueryPlanResolution.Input"/> = <c>null</c> to indicate the
    /// caller should use built-in defaults.
    /// </summary>
    public async Task<QueryPlanResolution> ResolveAsync(Guid? templateId, CancellationToken ct = default)
    {
        QueryPlanTemplate? template = null;

        if (templateId.HasValue)
            template = await _store.GetByIdAsync(templateId.Value, ct);

        // Fall back to the default template if no explicit one was provided or it wasn't found.
        template ??= await _store.GetDefaultAsync(ct);

        if (template == null)
            return new QueryPlanResolution(null, "Built-in Default");

        return new QueryPlanResolution(ToQueryPlanInput(template), template.Name);
    }

    private static QueryPlanInput ToQueryPlanInput(QueryPlanTemplate template) => new()
    {
        EhrDescription = template.EhrDescription,
        LookBack = template.LookBack,
        InitialQueries = template.InitialQueries.Select(ToQueryPlanQueryEntry).ToList(),
        SupplementalQueries = template.SupplementalQueries.Select(ToQueryPlanQueryEntry).ToList()
    };

    private static QueryPlanQueryEntry ToQueryPlanQueryEntry(QueryEntry src) => new()
    {
        ResourceType = src.ResourceType,
        QueryConfigType = src.QueryConfigType,
        OperationType = src.OperationType,
        Paged = src.Paged,
        Parameters = src.Parameters.Select(p => new QueryPlanParameterEntry
        {
            ParameterType = p.ParameterType,
            Name = p.Name,
            Variable = p.Variable,
            Format = p.Format,
            Literal = p.Literal,
            Resource = p.Resource,
            PagedValue = p.PagedValue
        }).ToList()
    };
}

/// <summary>
/// The query plan to use for a run, plus a human-readable name for log lines.
/// <see cref="Input"/> is <c>null</c> when the caller should use the built-in
/// <see cref="QueryPlanDefaults"/>.
/// </summary>
public sealed record QueryPlanResolution(QueryPlanInput? Input, string Name);
