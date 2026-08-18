using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

/// <summary>
/// Ensures the system default query plan template exists in MongoDB on application startup.
/// The system template is sourced from <see cref="QueryPlanDefaults.GetDefaultAsInput"/>
/// so the UI always stays in sync with the built-in automation defaults.
/// </summary>
public sealed class QueryPlanTemplateSeedService : IHostedService
{
    private static readonly Guid SystemDefaultId = new("00000000-0000-0000-1000-000000000001");

    private readonly IQueryPlanTemplateStore _store;
    private readonly ILogger<QueryPlanTemplateSeedService> _logger;

    public QueryPlanTemplateSeedService(IQueryPlanTemplateStore store, ILogger<QueryPlanTemplateSeedService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var defaultInput = QueryPlanDefaults.GetDefaultAsInput();

        var template = new QueryPlanTemplate
        {
            Id = SystemDefaultId,
            Name = "System Default",
            Description = "Built-in query plan that mirrors the automation pipeline defaults. " +
                          "Acquires Encounter, MedicationRequest, Location, Medication (initial) " +
                          "and Condition, Coverage, DiagnosticReport, Observation, Procedure, " +
                          "ServiceRequest, Device, Specimen (supplemental).",
            IsSystem = true,
            // Default selection is reconciled below so we never end up with multiple defaults.
            IsDefault = false,
            EhrDescription = defaultInput.EhrDescription ?? "Epic",
            LookBack = defaultInput.LookBack ?? "P0D",
            InitialQueries = defaultInput.InitialQueries.Select(ToQueryEntry).ToList(),
            SupplementalQueries = defaultInput.SupplementalQueries.Select(ToQueryEntry).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var existing = await _store.GetByIdAsync(SystemDefaultId, cancellationToken);
        if (existing == null)
        {
            await _store.UpsertAsync(template, cancellationToken);
            _logger.LogInformation("Seeded system default query plan template: {Id}", SystemDefaultId);
        }
        else
        {
            // Always overwrite to keep in sync with code.
            template.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.UpsertAsync(template, cancellationToken);
            _logger.LogDebug("Refreshed system default query plan template: {Id}", SystemDefaultId);
        }

        // Enforce single-default invariant.
        var allTemplates = await _store.GetAllAsync(cancellationToken);
        var selectedDefaultId = allTemplates
            .Where(t => t.IsDefault)
            // Prefer explicit user defaults over system template when duplicates exist.
            .OrderBy(t => t.IsSystem)
            .ThenByDescending(t => t.UpdatedAt)
            .Select(t => t.Id)
            .FirstOrDefault();

        if (selectedDefaultId == Guid.Empty)
            selectedDefaultId = SystemDefaultId;

        await _store.SetDefaultAsync(selectedDefaultId, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static QueryEntry ToQueryEntry(QueryPlanQueryEntry src) => new()
    {
        ResourceType = src.ResourceType,
        QueryConfigType = src.QueryConfigType,
        OperationType = src.OperationType,
        Paged = src.Paged,
        Parameters = src.Parameters.Select(p => new QueryParameter
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
