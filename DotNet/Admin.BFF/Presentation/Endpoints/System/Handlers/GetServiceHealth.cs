using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System.Handlers;

public static class GetServiceHealth
{
	public static async Task<IResult> Handle(HttpContext context,
		string service,
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
		var serviceName = HtmlInputSanitizer.SanitizeAndRemove(service)?.Trim();
		if (string.IsNullOrWhiteSpace(serviceName))
		{
			return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
				detail: "A service name is required.");
		}

		var cancellationToken = context.RequestAborted;
		cancellationToken.ThrowIfCancellationRequested();

		var serviceKey = serviceName.Replace(" ", string.Empty)
			.Replace("-", string.Empty).ToLowerInvariant();

		LinkServiceHealthReport? report;
		if (serviceKey == "adminbff")
		{
			var healthReport = await healthCheckService.CheckHealthAsync(cancellationToken);
			report = new LinkServiceHealthReport
			{
				Service = "Admin BFF",
				Status = healthReport.Status,
				TotalDuration = healthReport.TotalDuration,
				Entries = healthReport.Entries.ToDictionary(
					entry => entry.Key,
					entry => new LinkServiceHealthReportEntry
					{
						Status = entry.Value.Status,
						Duration = entry.Value.Duration,
						Description = entry.Value.Description
					})
			};
		}
		else
		{
			report = serviceKey switch
			{
				"account" => await accountService.LinkServiceHealthCheck(cancellationToken),
				"audit" => await auditService.LinkServiceHealthCheck(cancellationToken),
				"census" => await censusService.LinkServiceHealthCheck(cancellationToken),
				"dataacquisition" => await dataAcquisitionService.LinkServiceHealthCheck(cancellationToken),
				"normalization" => await normalizationService.LinkServiceHealthCheck(cancellationToken),
				"querydispatch" => await queryDispatchService.LinkServiceHealthCheck(cancellationToken),
				"report" => await reportService.LinkServiceHealthCheck(cancellationToken),
				"submission" => await submissionService.LinkServiceHealthCheck(cancellationToken),
				"tenant" => await tenantService.LinkServiceHealthCheck(cancellationToken),
				"measureeval" or "measureevaluation" => await measureEvalService.LinkServiceHealthCheck(cancellationToken),
				"validation" => await validationService.LinkServiceHealthCheck(cancellationToken),
				"terminology" => await terminologyService.LinkServiceHealthCheck(cancellationToken),
				_ => null
			};
		}

		return report is null
			? Results.Problem(statusCode: StatusCodes.Status404NotFound,
				detail: "The requested service was not found.")
			: Results.Ok(report);
	}
}
