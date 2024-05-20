using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class GetUserByEmail
    {
        public static async Task<IResult> Handle(HttpContext context, string email, 
            [FromServices] ILogger<UserEndpoints> logger, [FromServices] IGetUserByEmail query)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    return Results.BadRequest("A user email address is required");
                }

                var user = await query.Execute(email, context.RequestAborted);
                if (user is null)
                {
                    return Results.NotFound();
                }

                logger.LogFindUser(email, context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.Ok(user);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogFindUserException(email, ex.Message);
                throw;
            }            
        }
    }
}
