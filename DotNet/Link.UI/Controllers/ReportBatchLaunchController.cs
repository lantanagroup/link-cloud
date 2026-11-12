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
/// Starts one batch against one EHR system. The saved cap stays on the settings page.
/// </summary>
[Route("Automation/ReportBatches/Launch")]
public sealed class ReportBatchLaunchController(IServiceProvider services) : Controller
{
    public const string Unavailable =
        "Report batches are unavailable until the automation engine is ready.";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SetView();
        return View(await BuildPageAsync(null, [], null, null, [], cancellationToken));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        string? ehrSystem,
        List<Guid>? scenarioIds,
        int copies,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        SetView();
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        var decision = await DecideAsync(
            dispatcher,
            ehrSystem,
            scenarioIds ?? [],
            copies,
            maxConcurrency,
            cancellationToken);
        if (decision.Errors.Count > 0 || dispatcher is null)
        {
            return View(await BuildPageAsync(
                ehrSystem,
                scenarioIds ?? [],
                copies,
                maxConcurrency,
                decision.Errors,
                cancellationToken));
        }

        try
        {
            var batch = await dispatcher.StartBatchAsync(
                decision.EhrSystem,
                decision.ExpandedScenarioIds,
                maxConcurrency,
                cancellationToken);
            return Redirect($"/Automation/ReportBatches/{batch.BatchId:D}");
        }
        catch (InvalidOperationException ex)
        {
            var errors = new List<string>(decision.Errors) { ex.Message };
            return View(await BuildPageAsync(
                ehrSystem,
                scenarioIds ?? [],
                copies,
                maxConcurrency,
                errors,
                cancellationToken));
        }
    }

    private void SetView()
    {
        ViewData["Title"] = "Launch a batch";
        ViewData["AutomationSection"] = "batch-launch";
    }

    private async Task<ReportBatchLaunchPage> BuildPageAsync(
        string? selectedEhr,
        IReadOnlyList<Guid> selectedScenarioIds,
        int? copies,
        int? maxConcurrency,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken)
    {
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        var ready = dispatcher is not null;
        var savedCap = await SavedCapAsync(cancellationToken);
        var ehrSystems = await ProbeAsync(cancellationToken);
        var selected = string.IsNullOrWhiteSpace(selectedEhr)
            ? ehrSystems.FirstOrDefault(system => system.IsDefault)?.Name ?? ehrSystems.FirstOrDefault()?.Name
            : selectedEhr.Sanitize().Trim();
        var pageErrors = errors.Count > 0
            ? errors
            : ready ? [] : new[] { Unavailable };

        return new ReportBatchLaunchPage
        {
            Ready = ready,
            SavedCap = savedCap,
            Copies = copies ?? 1,
            MaxConcurrency = maxConcurrency ?? savedCap,
            SelectedEhr = selected,
            SelectedScenarioIds = selectedScenarioIds,
            Errors = pageErrors,
            EhrSystems = ehrSystems,
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

    private async Task<IReadOnlyList<ReportBatchLaunchEhr>> ProbeAsync(CancellationToken cancellationToken)
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

        var rows = new List<ReportBatchLaunchEhr>(systems.Count);
        for (var index = 0; index < systems.Count; index++)
        {
            var system = systems[index];
            var probe = probes[index];
            rows.Add(new ReportBatchLaunchEhr
            {
                Name = system.Name,
                FhirServerBase = system.FhirServerBase,
                IsDefault = string.Equals(system.Name, catalog.Default.Name, StringComparison.Ordinal),
                Ready = probe?.Ready == true,
                ReadinessError = probe is null
                    ? "Readiness could not be checked."
                    : probe.Ready ? null : probe.Error
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
        string? ehrSystem,
        IReadOnlyList<Guid> scenarioIds,
        int copies,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (dispatcher is null)
        {
            errors.Add(Unavailable);
            return new LaunchDecision(errors, [], null);
        }

        var requested = (ehrSystem ?? string.Empty).Sanitize().Trim();
        var catalog = services.GetService<EhrSystemCatalog>();
        var system = string.IsNullOrEmpty(requested) ? null : catalog?.Find(requested);
        if (system is null)
        {
            errors.Add("ehrSystem is required and must be a configured EHR system.");
        }
        else
        {
            var readiness = services.GetService<EhrSystemReadinessChecker>();
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
        }

        var selected = new List<Guid>();
        var seen = new HashSet<Guid>();
        var store = services.GetService<IScenarioStore>();
        if (scenarioIds.Count == 0)
            errors.Add("At least one scenario is required.");

        foreach (var id in scenarioIds)
        {
            if (!seen.Add(id))
                continue;
            if (id == Guid.Empty)
            {
                errors.Add("scenarioId must be a UUID.");
                continue;
            }

            selected.Add(id);
            var scenario = store is null ? null : await store.GetByIdAsync(id, cancellationToken);
            if (scenario is null)
            {
                errors.Add($"Scenario {id} was not found.");
                continue;
            }

            AddScenarioErrors(scenario, errors);
        }

        if (copies < 1)
            errors.Add("Copies must be a whole number of at least 1.");
        else if (selected.Count > 0
                 && (long)selected.Count * copies > ReportBatchConcurrencyPolicy.MaxScenariosPerBatch)
        {
            errors.Add(
                $"A batch can include at most {ReportBatchConcurrencyPolicy.MaxScenariosPerBatch} scenarios.");
        }

        var savedCap = await SavedCapAsync(cancellationToken);
        try
        {
            ReportBatchConcurrencyPolicy.ResolveBatchCap(maxConcurrency, savedCap);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }

        if (errors.Count > 0 || system is null)
            return new LaunchDecision(errors, [], system?.Name);

        var expanded = new List<Guid>(checked(selected.Count * copies));
        foreach (var id in selected)
        {
            for (var copy = 0; copy < copies; copy++)
                expanded.Add(id);
        }

        return new LaunchDecision(errors, expanded, system.Name);
    }

    private static void AddScenarioErrors(TestScenarioDefinition scenario, List<string> errors)
    {
        var name = string.IsNullOrWhiteSpace(scenario.Name) ? scenario.Id.ToString() : scenario.Name.Trim();
        var start = scenario.ReportPeriodStart;
        var end = scenario.ReportPeriodEnd;
        // Both ends null is valid. The run uses the system default window.
        if ((start is null) != (end is null))
            errors.Add($"{name} needs both ends of the report period.");
        else if (start is not null && end is not null && start.Value > end.Value)
            errors.Add($"{name} has a report period that starts after it ends.");

        if (!HasPatientSource(scenario))
            errors.Add($"{name} needs a patient source.");
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
        IReadOnlyList<Guid> ExpandedScenarioIds,
        string? EhrSystem);
}
