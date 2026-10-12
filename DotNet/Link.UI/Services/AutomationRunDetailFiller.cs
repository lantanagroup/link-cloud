using Link.UI.Models;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
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