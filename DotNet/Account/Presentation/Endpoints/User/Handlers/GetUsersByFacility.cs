using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class GetUsersByFacility
    {
        public static async Task<IResult> Handle(HttpContext context, string id, 
            [FromServices] ILogger<UserEndpoints> logger, [FromServices] IGetFacilityUsers query)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Results.BadRequest("A facility id is required");
                }

                var users = await query.Execute(id, context.RequestAborted);

                logger.LogFindUsers(context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogFindUsersException(ex.Message);
                throw;
            }            
        }
    }
}
