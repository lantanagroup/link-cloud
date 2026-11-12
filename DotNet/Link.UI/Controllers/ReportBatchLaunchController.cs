using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Link.UI.Services;
using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Starts one batch per selected EHR system and opens the combined launch page.
/// The saved cap stays on the settings page. Each batch may only lower it.
/// </summary>
[Route("Automation/ReportBatches/Launch")]
public sealed class ReportBatchLaunchController(IServiceProvider services) : Controller
{
    public const string Unavailable =
        "Report batches are unavailable until the automation engine is ready.";

    public const string FacilityTemplateFallback = "System default";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SetView();
        return View(await BuildPageAsync(null, [], cancellationToken));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        List<ReportBatchLaunchEhrForm>? ehrs,
        CancellationToken cancellationToken)
    {
        SetView();
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        var decision = await DecideAsync(dispatcher, ehrs, cancellationToken);
        if (decision.Errors.Count > 0 || dispatcher is null)
        {
            return View(await BuildPageAsync(ehrs, decision.Errors, cancellationToken));
        }

        try
        {
            var launch = await dispatcher.StartLaunchAsync(decision.Slices, cancellationToken);
            return Redirect(ReportBatchDisplay.LaunchPath(launch.LaunchId));
        }
        catch (InvalidOperationException ex)
        {
            var errors = new List<string>(decision.Errors) { ex.Message };
            return View(await BuildPageAsync(ehrs, errors, cancellationToken));
        }
    }

    private void SetView()
    {
        ViewData["Title"] = "Launch";
        ViewData["AutomationSection"] = "batch-launch";
    }

    private async Task<ReportBatchLaunchPage> BuildPageAsync(
        IReadOnlyList<ReportBatchLaunchEhrForm>? posted,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken)
    {
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        var ready = dispatcher is not null;
        var savedCap = await SavedCapAsync(cancellationToken);
        var pageErrors = errors.Count > 0
            ? errors
            : ready ? [] : new[] { Unavailable };

        return new ReportBatchLaunchPage
        {
            Ready = ready,
            SavedCap = savedCap,
            Errors = pageErrors,
            EhrSystems = await ProbeAsync(posted, savedCap, cancellationToken),
            Scenarios = await LoadScenariosAsync(cancellationToken)
        };
    }

    private async Task<int> SavedCapAsync(CancellationToken cancellationToken)
    {
        var settings = services.GetService<IReportBatchSettingsStore>();
        return settings is null
            ? ReportBatchConcurrencyPolicy.DefaultMaxConcurrency
            : await settings.GetMaxConcurrencyAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ReportBatchLaunchEhr>> ProbeAsync(
        IReadOnlyList<ReportBatchLaunchEhrForm>? posted,
        int savedCap,
        CancellationToken cancellationToken)
    {
        var catalog = services.GetService<EhrSystemCatalog>();
        if (catalog is null)
            return [];

        var readiness = services.GetService<EhrSystemReadinessChecker>();
        var systems = catalog.Systems;
        var probes = new EhrReadinessResult?[systems.Count];
        if (readiness is not null && systems.Count > 0)
        {
            await Task.WhenAll(systems.Select(async (system, index) =>
            {
                probes[index] = await readiness.CheckAsync(system, cancellationToken);
            }));
        }

        var defaultName = catalog.Default.Name;
        var rows = new List<ReportBatchLaunchEhr>(systems.Count);
        for (var index = 0; index < systems.Count; index++)
        {
            var system = systems[index];
            var probe = probes[index];
            var match = posted?.FirstOrDefault(row =>
                string.Equals(Clean(row.Name), system.Name, StringComparison.OrdinalIgnoreCase));
            rows.Add(new ReportBatchLaunchEhr
            {
                Name = system.Name,
                FhirServerBase = system.FhirServerBase,
                IsDefault = string.Equals(system.Name, defaultName, StringComparison.Ordinal),
                Ready = probe?.Ready == true,
                ReadinessError = probe is null
                    ? "Readiness could not be checked."
                    : probe.Ready ? null : probe.Error,
                FacilityTemplate = string.IsNullOrWhiteSpace(system.DefaultFacilityTemplate)
                    ? FacilityTemplateFallback
                    : system.DefaultFacilityTemplate,
                Selected = posted is null
                    ? string.Equals(system.Name, defaultName, StringComparison.Ordinal)
                    : match?.Selected == true,
                Copies = match?.Copies ?? 1,
                MaxConcurrency = match?.MaxConcurrency is int postedCap && postedCap > 0 ? postedCap : savedCap,
                SelectedScenarioIds = match?.ScenarioIds ?? []
            });
        }

        return rows;
    }

    private async Task<IReadOnlyList<ReportBatchLaunchScenario>> LoadScenariosAsync(
        CancellationToken cancellationToken)
    {
        var store = services.GetService<IScenarioStore>();
        if (store is null)
            return [];

        var scenarios = await store.GetAllAsync(cancellationToken);
        return scenarios
            .OrderBy(scenario => IsScheduled(scenario.ReportMethod) ? 1 : 0)
            .ThenBy(scenario => scenario.Name, StringComparer.OrdinalIgnoreCase)
            .Select(scenario => new ReportBatchLaunchScenario
            {
                Id = scenario.Id,
                Name = scenario.Name,
                Group = IsScheduled(scenario.ReportMethod) ? "Scheduled" : "Ad-hoc",
                ReportMethodLabel = MethodLabel(scenario.ReportMethod),
                IsSystemScenario = scenario.IsSystemScenario
            })
            .ToList();
    }

    private async Task<LaunchDecision> DecideAsync(
        ReportBatchDispatcher? dispatcher,
        IReadOnlyList<ReportBatchLaunchEhrForm>? posted,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (dispatcher is null)
        {
            errors.Add(Unavailable);
            return new LaunchDecision(errors, []);
        }

        var selected = (posted ?? []).Where(row => row.Selected).ToList();
        if (selected.Count == 0)
        {
            errors.Add("At least one EHR system is required.");
            return new LaunchDecision(errors, []);
        }

        var catalog = services.GetService<EhrSystemCatalog>();
        var readiness = services.GetService<EhrSystemReadinessChecker>();
        var store = services.GetService<IScenarioStore>();
        var savedCap = await SavedCapAsync(cancellationToken);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slices = new List<ReportBatchLaunchSlice>();

        foreach (var row in selected)
        {
            var requested = Clean(row.Name);
            var system = string.IsNullOrEmpty(requested) ? null : catalog?.Find(requested);
            if (system is null)
            {
                errors.Add(string.IsNullOrEmpty(requested)
                    ? "ehrSystem is required and must be a configured EHR system."
                    : $"{requested} is not a configured EHR system.");
                continue;
            }

            if (!seen.Add(system.Name))
            {
                errors.Add($"{system.Name} is selected more than once.");
                continue;
            }

            if (readiness is null)
            {
                errors.Add($"{system.Name} is not ready.");
            }
            else
            {
                var probe = await readiness.CheckAsync(system, cancellationToken);
                if (!probe.Ready)
                {
                    errors.Add(string.IsNullOrWhiteSpace(probe.Error)
                        ? $"{system.Name} is not ready."
                        : $"{system.Name} is not ready. {probe.Error}");
                }
            }

            var chosen = new List<Guid>();
            var ids = new HashSet<Guid>();
            if (row.ScenarioIds is null || row.ScenarioIds.Count == 0)
            {
                errors.Add($"{system.Name} needs at least one scenario.");
            }
            else
            {
                foreach (var id in row.ScenarioIds)
                {
                    if (!ids.Add(id))
                        continue;
                    if (id == Guid.Empty)
                    {
                        errors.Add($"{system.Name}: scenarioId must be a UUID.");
                        continue;
                    }

                    chosen.Add(id);
                    var scenario = store is null ? null : await store.GetByIdAsync(id, cancellationToken);
                    if (scenario is null)
                    {
                        errors.Add($"{system.Name}: Scenario {id} was not found.");
                        continue;
                    }

                    AddScenarioErrors(system.Name, scenario, errors);
                }
            }

            if (row.Copies < 1)
                errors.Add($"{system.Name}: Copies must be a whole number of at least 1.");
            else if (chosen.Count > 0
                     && (long)chosen.Count * row.Copies > ReportBatchConcurrencyPolicy.MaxScenariosPerBatch)
            {
                errors.Add(
                    $"{system.Name}: A batch can include at most {ReportBatchConcurrencyPolicy.MaxScenariosPerBatch} scenarios.");
            }

            try
            {
                ReportBatchConcurrencyPolicy.ResolveBatchCap(row.MaxConcurrency, savedCap);
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"{system.Name}: {ex.Message}");
            }

            if (errors.Count > 0 || chosen.Count == 0 || row.Copies < 1)
                continue;

            var expanded = new List<Guid>(checked(chosen.Count * row.Copies));
            foreach (var id in chosen)
            {
                for (var copy = 0; copy < row.Copies; copy++)
                    expanded.Add(id);
            }

            slices.Add(new ReportBatchLaunchSlice(system.Name, expanded, row.MaxConcurrency));
        }

        if (errors.Count > 0)
            return new LaunchDecision(errors, []);

        return new LaunchDecision(errors, slices);
    }

    private static void AddScenarioErrors(string ehr, TestScenarioDefinition scenario, List<string> errors)
    {
        var name = string.IsNullOrWhiteSpace(scenario.Name) ? scenario.Id.ToString() : scenario.Name.Trim();
        var start = scenario.ReportPeriodStart;
        var end = scenario.ReportPeriodEnd;
        // Both ends null is valid. The run uses the system default window.
        if ((start is null) != (end is null))
            errors.Add($"{ehr}: {name} needs both ends of the report period.");
        else if (start is not null && end is not null && start.Value > end.Value)
            errors.Add($"{ehr}: {name} has a report period that starts after it ends.");

        if (!HasPatientSource(scenario))
            errors.Add($"{ehr}: {name} needs a patient source.");
    }

    private static bool HasPatientSource(TestScenarioDefinition scenario)
    {
        if (scenario.PatientCount > 0)
            return true;
        if (scenario.PatientCohorts is not null && scenario.PatientCohorts.Any(cohort => cohort.PatientCount > 0))
            return true;
        return HasImport(scenario.ImportedPatientIds) || HasImport(scenario.ImportedPatientBundles);
    }

    private static bool HasImport(IEnumerable<ImportedPatientInput>? patients)
    {
        if (patients is null)
            return false;

        foreach (var patient in patients)
        {
            if (!string.IsNullOrWhiteSpace(patient.PatientId)
                || patient.UploadedBundleId is not null
                || !string.IsNullOrWhiteSpace(patient.BundleJson))
            {
                return true;
            }
        }

        return false;
    }

    private static string Clean(string? value) => (value ?? string.Empty).Sanitize().Trim();

    private static bool IsScheduled(ReportMethod method)
        => ReportExecution.UsesCensusScheduleKickoff(method);

    private static string MethodLabel(ReportMethod method) => method switch
    {
        ReportMethod.Adhoc => "Ad-hoc",
        ReportMethod.ScheduledReport => "Scheduled report",
        ReportMethod.RegenerateReport => "Regenerate report",
        _ => method.ToString()
    };

    private sealed record LaunchDecision(
        IReadOnlyList<string> Errors,
        IReadOnlyList<ReportBatchLaunchSlice> Slices);
}
