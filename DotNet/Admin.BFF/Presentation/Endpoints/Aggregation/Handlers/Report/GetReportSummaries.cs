using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Net;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;

public static class GetReportSummaries
{
    public static async Task<IResult> Search(
        ILoggerFactory loggerFactory,
        HttpContext context,
        ReportService reportService,
        string? facilityId = null,
        Frequency? frequency = null,
        string? reportType = null,
        DateTime? reportStartDate = null,
        DateTime? reportEndDate = null,
        ScheduleStatus[]? status = null,
        bool? endOfReportPeriodJobHasRun = null,
        bool includeDeleted = false,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageNumber = 1,
        int pageSize = 10,
        DateOnly? createDate = null,
        string? reportScheduleId = null
        )
    {
        try
        {
            var logger = loggerFactory.CreateLogger("GetReportSummaries");

            //TODO: add validation for facilityId

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var response = await reportService.ReportSummaryList(context.User,
                context.RequestAborted,
                facilityId,
                frequency,
                reportType,
                reportStartDate,
                reportEndDate,
                statuses: status,
                endOfReportPeriodJobHasRun,
                includeDeleted,
                sortBy,
                sortOrder,
                pageNumber,
                pageSize,
                createDate,
                reportScheduleId
                );

            if (!response.IsSuccessStatusCode)
                return response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => Results.Unauthorized(),
                    HttpStatusCode.Forbidden => Results.Forbid(),
                    _ => Results.Problem("An error occurred while processing your request.",
                        statusCode: (int)response.StatusCode)
                };

            var summaries = await response.Content.ReadFromJsonAsync<PagedConfigModel<ScheduledReportListSummary>>(cancellationToken: context.RequestAborted);
            if (summaries != null)
            {
                foreach (var summary in summaries.Records)
                {
                    await PopulateSummaryCounts(context, reportService, summary);
                }
            }


            return summaries is null ? Results.NotFound("No report summaries found.") : Results.Ok(summaries);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> Get(
        ILoggerFactory loggerFactory,
        HttpContext context,
        ReportService reportService,
        string reportScheduleId)
    {
        try
        {
            var logger = loggerFactory.CreateLogger("GetReportSummaries");

            if (string.IsNullOrWhiteSpace(reportScheduleId))
                return Results.BadRequest("Report schedule ID is required");

            var response = await reportService.GetReportScheduleById(
                context.User,
                context.RequestAborted,
                reportScheduleId);

            if (!response.IsSuccessStatusCode)
                return response.StatusCode switch
                {
                    HttpStatusCode.NotFound => Results.NotFound($"Report schedule with ID '{reportScheduleId}' not found."),
                    HttpStatusCode.Unauthorized => Results.Unauthorized(),
                    HttpStatusCode.Forbidden => Results.Forbid(),
                    _ => Results.Problem("An error occurred while processing your request.",
                        statusCode: (int)response.StatusCode)
                };

            var summary = await response.Content.ReadFromJsonAsync<ScheduledReportListSummary>(cancellationToken: context.RequestAborted);

            if (summary != null)
            {
                await PopulateSummaryCounts(context, reportService, summary);
            }

            return summary is null ? Results.NotFound($"Report schedule with ID '{reportScheduleId}' not found.") : Results.Ok(summary);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task PopulateSummaryCounts(HttpContext context, ReportService reportService, ScheduledReportListSummary summary)
    {
        var patientInCensusCountResponse = await reportService.GetPatientInCensusCount(context.User, context.RequestAborted, summary.Id.ToString());
        if (patientInCensusCountResponse.IsSuccessStatusCode)
        {
            var content = await patientInCensusCountResponse.Content.ReadAsStringAsync();
            summary.CensusCount = int.Parse(content);
        }

        var populationResponse = await reportService.GetReportPopulationsByReportScheduleId(context.User, context.RequestAborted, summary.Id.ToString());
        if (populationResponse.IsSuccessStatusCode)
        {
            var content = await populationResponse.Content.ReadAsStringAsync();
            summary.InitialPopulationCount = int.Parse(content);
        }
    }
}