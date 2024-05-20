using LantanaGroup.Link.Account.Application.Commands.User;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class UpdateExistingUser
    {
        public static async Task<IResult> Handle(HttpContext context, string id, LinkUserModel model, 
            [FromServices] ILogger<UserEndpoints> logger, [FromServices] IGetUserByid queryUser, 
            [FromServices] ICreateUser createUserCommand, [FromServices] IUpdateUser command)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Results.BadRequest("A user id is required");
                }

                //check that the id in the url matches the id in the model
                if (model.Id != id)
                {
                    return Results.BadRequest("The user id in the url does not match the user id in the user model provided.");
                }

                var requestor = context.User;
                var existingUser = await queryUser.Execute(id, cancellationToken: context.RequestAborted);
                if (existingUser is null)
                {
                    //create new user
                    var createdUser = await createUserCommand.Execute(requestor, model, context.RequestAborted);

                    logger.LogUserCreated(createdUser.Id, requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                    //build resource uri
                    var uriBuilder = new UriBuilder
                    {
                        Scheme = context.Request.Scheme,
                        Host = context.Request.Host.Host,
                        Path = $"api/account/user/{createdUser.Id}"
                    };

                    if (context.Request.Host.Port.HasValue)
                    {
                        uriBuilder.Port = context.Request.Host.Port.Value;
                    }

                    return Results.Created(uriBuilder.ToString(), createdUser);
                }

                //update an existing user
                var updateResult = await command.Execute(requestor, model, context.RequestAborted);
                if (!updateResult)
                {
                    return Results.Problem("Failed to update user");
                }

                logger.LogUpdateUser(id, context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogUpdateUserException(id, ex.Message);
                throw;
            }            
        }
    }
}
