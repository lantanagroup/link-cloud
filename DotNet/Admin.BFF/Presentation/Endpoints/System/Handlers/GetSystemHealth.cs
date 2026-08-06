using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System.Handlers;

public static class GetSystemHealth
{
    public static async Task<IResult> Handle(HttpContext context,
        HealthCheckService healthCheckService,
        AccountService accountService,
        AuditService auditService,
        CensusService censusService,
        DataAcquisitionService dataAcquisitionService,
        NormalizationService normalizationService,
        QueryDispatchService queryDispatchService,
        ReportService reportService,
        SubmissionService submissionService,
        TenantService tenantService,
        MeasureEvalService measureEvalService,
        ValidationService validationService,
        TerminologyService terminologyService)
    {

        var dotNetHealthCheckTasks = new List<Task<LinkServiceHealthReport>>
        {
            accountService.LinkServiceHealthCheck(context.RequestAborted),
            auditService.LinkServiceHealthCheck(context.RequestAborted),
            censusService.LinkServiceHealthCheck(context.RequestAborted),
            dataAcquisitionService.LinkServiceHealthCheck(context.RequestAborted),
            normalizationService.LinkServiceHealthCheck(context.RequestAborted),
            queryDispatchService.LinkServiceHealthCheck(context.RequestAborted),
            reportService.LinkServiceHealthCheck(context.RequestAborted),
            submissionService.LinkServiceHealthCheck(context.RequestAborted),
            tenantService.LinkServiceHealthCheck(context.RequestAborted),
            terminologyService.LinkServiceHealthCheck(context.RequestAborted)
        };

        // Get Admin BFF health
        var bffHealthReport = await healthCheckService.CheckHealthAsync(context.RequestAborted);
        var bffLinkReport = new LinkServiceHealthReport
        {
            Service = "Admin BFF",
            Status = bffHealthReport.Status,
            TotalDuration = bffHealthReport.TotalDuration,
            Entries = bffHealthReport.Entries.ToDictionary(
                x => x.Key,
                x => new LinkServiceHealthReportEntry
                {
                    Status = x.Value.Status,
                    Duration = x.Value.Duration
                })
        };

        //if we upgrade to .NET 9, we can use Task.WhenEach
        var results = await Task.WhenAll(dotNetHealthCheckTasks);

        //TODO: improve integration with java services
        var measureEvalHealthCheckResult = await measureEvalService.LinkServiceHealthCheck(context.RequestAborted);
        var measureEvalHealthSummary = LinkServiceHealthReportExtensions.FromDomain(measureEvalHealthCheckResult);
        var validationHealthCheckResult = await validationService.LinkServiceHealthCheck(context.RequestAborted);
        var validationHealthSummary = LinkServiceHealthReportExtensions.FromDomain(validationHealthCheckResult);

        var healthSummary = results.Select(LinkServiceHealthReportExtensions.FromDomain).ToList();
        healthSummary.Add(LinkServiceHealthReportExtensions.FromDomain(bffLinkReport));
        healthSummary.Add(measureEvalHealthSummary);
        healthSummary.Add(validationHealthSummary);

        return Results.Ok(healthSummary);
    }
}