using System.Net;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;

public static class GetReportSummaries
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ReportService reportService,
        string? facilityId,
        int pageNumber = 1,
        int pageSize = 10
        )
    {
        try
        {
            if(pageNumber < 1) return Results.BadRequest("Page number must be greater than 0");
            if(pageSize < 1) return Results.BadRequest("Page size must be greater than 0");
            
            var response = await reportService.ReportSummaryList(facilityId, pageNumber, pageSize, context.RequestAborted);

            if (!response.IsSuccessStatusCode)
                return response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => Results.Unauthorized(),
                    HttpStatusCode.Forbidden => Results.Forbid(),
                    _ => Results.Problem("An error occurred while processing your request.",
                        statusCode: (int)response.StatusCode)
                };
            
            var summaries = await response.Content.ReadFromJsonAsync<PagedConfigModel<ScheduledReportListSummary>>(cancellationToken: context.RequestAborted);

            //TODO: combine with other service information
            
            return Results.Ok(summaries);

        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}