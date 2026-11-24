using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;
using Link.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Services;

/// <summary>Everything a run detail section needs besides the run itself. Shared by the run page and the report page.</summary>
public static class AutomationRunDetailFiller
{
    public static async Task FillAsync(Controller controller, IServiceProvider services, ILogger logger, LantanaGroup.Link.Automation.Link.Models.AutomationRunSummary detail, CancellationToken cancellationToken)
    {
        controller.ViewBag.TemplateCacheVersionNumber = detail.GeneratedTemplateCacheVersionNumber;
        try
        {
            var patients = services.GetService<IPatientConfigurationStore>();
            if (patients is not null)
                controller.ViewBag.PatientConfigurations = await patients.GetAllAsync(cancellationToken);
            if (detail.IsMetricsRun)
            {
                var presenter = services.GetService<MetricsRunPresenter>();
                if (presenter is not null)
                {
                    var captured = await presenter.GetCapturedAsync(detail.RunId, cancellationToken);
                    if (captured?.PreviousRunId is Guid previous)
                        captured.PreviousRunHref = controller.Url.Action("Run", "Automation", new { id = previous });
                    controller.ViewBag.Performance = captured;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Run detail catalogs could not be loaded for {RunId}.", detail.RunId);
        }

        controller.ViewData["SuggestedWorkaroundJson"] = SerializeForPage(
            await LoadSuggestedWorkaroundAsync(services, controller, logger, detail, cancellationToken));
    }

    /// <summary>
    /// One suggestion for a failed run whose stored organization-location counts confirm the rule.
    /// A run that is not failed, or that does not confirm a rule, is null.
    /// </summary>
    public static async Task<SuggestedWorkaround?> LoadSuggestedWorkaroundAsync(
        IServiceProvider services,
        Controller controller,
        ILogger logger,
        AutomationRunSummary run,
        CancellationToken cancellationToken)
    {
        if (run.Status != AutomationRunStatus.Failed)
            return null;

        var stored = await ReadOrgLocationAsync<OrgLocationSummary>(services, logger, run.RunId, cancellationToken);
        StoreBackedServicePoller.OrgLocationSnapshot? legacy = null;
        if (stored == null || stored.ConfigurationCount + stored.LocationMappingCount + stored.EncounterMappingCount == 0)
            legacy = await ReadOrgLocationAsync<StoreBackedServicePoller.OrgLocationSnapshot>(services, logger, run.RunId, cancellationToken);

        GenerationManifestSnapshot? manifest = null;
        if (services.GetService<IAutomationRunManager>() is { } manager)
        {
            try
            {
                manifest = await manager.GetGenerationManifestAsync(run.RunId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Organization resource map manifest could not be read for a suggested workaround ({ExceptionType}).",
                    ex.GetType().Name);
            }
        }

        var mapId = OrganizationMapId(manifest, run.RunConfigurationJson);
        OrganizationResourceMapTemplate? map = null;
        if (mapId.HasValue && services.GetService<IOrganizationResourceMapTemplateStore>() is { } maps)
        {
            try
            {
                map = await maps.GetByIdAsync(mapId.Value, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Organization resource map could not be read for a suggested workaround ({ExceptionType}).",
                    ex.GetType().Name);
            }
        }

        return MatchStored(
            run,
            stored,
            legacy,
            manifest,
            map,
            controller.Url?.Action("Index", "OrganizationResourceMaps"));
    }

    public static SuggestedWorkaround? MatchStored(
        AutomationRunSummary run,
        OrgLocationSummary? stored,
        StoreBackedServicePoller.OrgLocationSnapshot? legacy,
        GenerationManifestSnapshot? manifest,
        OrganizationResourceMapTemplate? map,
        string? organizationMapsUrl)
    {
        if (run.Status != AutomationRunStatus.Failed)
            return null;

        return SuggestedWorkaroundBuilder.Match(new SuggestedWorkaroundInput
        {
            Status = run.Status,
            Summary = SuggestedWorkaroundBuilder.ResolveOrgLocation(stored, legacy),
            AppliedMapName = manifest?.AppliedOrganizationResourceMapName,
            Map = map,
            OrganizationMapsUrl = organizationMapsUrl
        });
    }

    internal static Guid? OrganizationMapId(GenerationManifestSnapshot? manifest, string? runConfigurationJson) =>
        manifest?.AppliedOrganizationResourceMapTemplateId
            ?? RunConfigurationReader.Read(runConfigurationJson).OrganizationMapId;

    public static string SerializeForPage(SuggestedWorkaround? workaround)
    {
        var json = JsonSerializer.Serialize(workaround, WorkaroundJson);
        return json.Replace("<", "\\u003c", StringComparison.Ordinal);
    }

    private static readonly JsonSerializerOptions WorkaroundJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<T?> ReadOrgLocationAsync<T>(
        IServiceProvider services,
        ILogger logger,
        Guid runId,
        CancellationToken cancellationToken)
        where T : class
    {
        if (services.GetService<ISnapshotStore>() is not { } snapshots)
            return null;

        try
        {
            return (await snapshots.GetDomainAsync<T>(runId, "orgLocation", cancellationToken))?.Data;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Stored organization location counts could not be read for a suggested workaround ({ExceptionType}).",
                ex.GetType().Name);
            return null;
        }
    }

    /// <summary>Loads the run and its live detail; never throws for a missing or unreadable run.</summary>
    public static async Task<AutomationRunPage?> LoadAsync(Controller controller, IServiceProvider services, ILogger logger, Guid runId, CancellationToken cancellationToken)
    {
        var reader = services.GetService<AutomationRunReader>();
        if (reader is null)
            return null;
        try
        {
            var page = await reader.LoadRunAsync(runId, cancellationToken);
            var manager = services.GetService<IAutomationRunManager>();
            if (page.Found && manager is not null)
            {
                page.Detail = await manager.GetRunForDisplayAsync(runId, cancellationToken);
                if (page.Detail is not null)
                    await FillAsync(controller, services, logger, page.Detail, cancellationToken);
            }
            return page;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Run {RunId} could not be loaded for the report page.", runId);
            return null;
        }
    }
}