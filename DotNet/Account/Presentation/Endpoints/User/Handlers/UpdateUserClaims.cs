using LantanaGroup.Link.Account.Application.Commands.User;
using LantanaGroup.Link.Account.Application.Models;
using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class UpdateUserClaims
    {
        public static async Task<IResult> Handle(HttpContext context, Guid id, LinkClaimsModel model,
            [FromServices] ILogger<UserEndpoints> logger, [FromServices] IGetUserByid queryUser, [FromServices] IUpdateUserClaims command)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return Results.BadRequest("A user id is required");
                }

                //check if user exists
                var user = await queryUser.Execute(id, cancellationToken: context.RequestAborted);
                if (user is null)
                {
                    return Results.NotFound();
                }

                var requestor = context.User;
                var outcome = await command.Execute(requestor, id, model.Claims, context.RequestAborted);
                if (!outcome)
                {
                    return Results.Problem("Failed to update user claims");
                }
                
                user.UserClaims = model.Claims;
                
                logger.LogUpdateUser(user.Id.ToString(), requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.Ok(user);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogUpdateUserException(id.ToString(), ex.Message);
                throw;
            }
            
        }
    }
}
