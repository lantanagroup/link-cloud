using LantanaGroup.Link.Account.Application.Commands.User;
using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class RecoverDeletedUser
    {
        public static async Task<IResult> Handle(HttpContext context, Guid id,
            [FromServices] ILogger<UserEndpoints> logger, [FromServices] IGetUserByid queryUser, [FromServices] IRecoverUser command)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return Results.BadRequest("A user id is required");
                }

                var requestor = context.User;

                var user = await queryUser.Execute(id, cancellationToken: context.RequestAborted);
                if (user is null)
                {
                    return Results.NotFound();
                }

                var outcome = await command.Execute(requestor, id, context.RequestAborted);
                if (outcome is null)
                {
                    return Results.Problem("Failed to recover deleted user");
                }

                logger.LogUserRecovery(id.ToString(), requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.Ok(outcome);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogUserRecoveryException(id.ToString(), ex.Message);
                throw;
            }            
        }
    }
}
