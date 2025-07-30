using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System.Hanlders;

public static class GetSystemHealth
{
    public static async Task<IResult> Handle(HttpContext context, 
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
        ValidationService validationService)
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
            tenantService.LinkServiceHealthCheck(context.RequestAborted)
        };

        //if we upgrade to .NET 9, we can use Task.WhenEach
        var results = await Task.WhenAll(dotNetHealthCheckTasks);
        
        //TODO: improve integration with java services
        var measureEvalHealthCheckResult = await measureEvalService.LinkServiceHealthCheck(context.RequestAborted);
        var measureEvalHealthSummary = LinkServiceHealthReportExtensions.FromDomain(measureEvalHealthCheckResult);
        measureEvalHealthSummary.CacheConnection = LinkServiceHealthStatus.NotApplicable;
        var validationHealthCheckResult = await validationService.LinkServiceHealthCheck(context.RequestAborted);
        var validationHealthSummary = LinkServiceHealthReportExtensions.FromDomain(validationHealthCheckResult);
        validationHealthSummary.KafkaConnection = LinkServiceHealthStatus.Unknown;
        validationHealthSummary.DatabaseConnection = LinkServiceHealthStatus.Unknown;
        validationHealthSummary.CacheConnection = LinkServiceHealthStatus.Unknown;
        
        var healthSummary = results.Select(LinkServiceHealthReportExtensions.FromDomain).ToList();
        healthSummary.Add(measureEvalHealthSummary);
        healthSummary.Add(validationHealthSummary);
        
        return Results.Ok(healthSummary);
    }
}