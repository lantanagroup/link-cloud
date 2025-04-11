using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using System.Net;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Tenant;

public static class PostAdhocReportRequested
{
    public static async Task<IResult> Handle(HttpContext context, TenantService tenantService, string facilityId, AdHocReportRequest request)
    {
        try
        {
            var response = await tenantService.GenerateAdHocReport(context.User, facilityId, request);

            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => Results.Unauthorized(),
                    HttpStatusCode.Forbidden => Results.Forbid(),
                    _ => Results.Problem("An error occurred while processing your request.",
                        statusCode: (int)response.StatusCode)
                };
            }

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}