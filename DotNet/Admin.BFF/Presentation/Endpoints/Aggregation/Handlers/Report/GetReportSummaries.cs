using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Net;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;

public static class GetReportSummaries
{
    public static async Task<IResult> Handle(
        ILoggerFactory loggerFactory,
        HttpContext context,
        ReportService reportService,
        string? facilityId = null,
        Frequency? frequency = null,
        string? reportType = null,
        DateTime? reportStartDate = null,
        DateTime? reportEndDate = null,
        ScheduleStatus? status = null,
        bool? endOfReportPeriodJobHasRun = null,
        bool includeDeleted = false,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageNumber = 1,
        int pageSize = 10
        )
    {
        try
        {
            var logger = loggerFactory.CreateLogger("GetReportSummaries");
            
            //TODO: add validation for facilityId
            
            if(pageNumber < 1) return Results.BadRequest("Page number must be greater than 0");
            if(pageSize < 1) return Results.BadRequest("Page size must be greater than 0");
            
            var response = await reportService.ReportSummaryList(context.User, 
                context.RequestAborted,
                facilityId, 
                frequency,
                reportType, 
                reportStartDate, 
                reportEndDate,
                status,
                endOfReportPeriodJobHasRun,
                includeDeleted,
                sortBy,
                sortOrder,
                pageNumber,
                pageSize
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
            if(summaries != null)
            {
                foreach(var summary in summaries.Records)
                {
                    var patientInCensusCountResponse = await reportService.GetPatientInCensusCount(context.User, context.RequestAborted, summary.Id);
                    if (patientInCensusCountResponse.IsSuccessStatusCode)
                    {
                        var content = await patientInCensusCountResponse.Content.ReadAsStringAsync();
                        summary.CensusCount = int.Parse(content);
                    }

                    var populationResponse = await reportService.GetReportPopulationsByReportScheduleId(context.User, context.RequestAborted, summary.Id);
                    if(populationResponse.IsSuccessStatusCode)
                    {
                        var content = await populationResponse.Content.ReadAsStringAsync();
                        summary.InitialPopulationCount = int.Parse(content);
                    }
                }
            }


            return summaries is null ? Results.NotFound("No report summaries found.") : Results.Ok(summaries);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}