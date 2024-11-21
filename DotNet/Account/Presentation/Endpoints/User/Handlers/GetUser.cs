using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class GetUser
    {
        public static async Task<IResult> Handle(HttpContext context, Guid id, 
            [FromServices] ILogger<UserEndpoints> logger, [FromServices] IGetUserByid query)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return Results.BadRequest("A user id is required");
                }

                var user = await query.Execute(id, context.RequestAborted);
                if (user is null)
                {
                    return Results.NotFound();
                }

                logger.LogFindUser(id.ToString(), context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.Ok(user);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogFindUserException(id.ToString(), ex.Message);
                throw;
            }
            
        }
    }
}
