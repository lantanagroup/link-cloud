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
        public static async Task<IResult> Handle(HttpContext context, Guid id, LinkUserModel model, 
            [FromServices] ILogger<UserEndpoints> logger, [FromServices] IGetUserByid queryUser, [FromServices] IGetUserByEmail queryUserByEmail,
            [FromServices] ICreateUser createUserCommand, [FromServices] IUpdateUser command)
        {
            try
            {
                if (id == Guid.Empty)
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

                    //verify that the emial is not already in use
                    var existingUserByEmail = await queryUserByEmail.Execute(model.Email, context.RequestAborted);
                    if (existingUserByEmail is not null)
                    {
                        return Results.Conflict("A user with the same email already exists.");
                    }

                    //create new user
                    var createdUser = await createUserCommand.Execute(requestor, model, context.RequestAborted);

                    logger.LogUserCreated(createdUser.Id.ToString(), requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

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
                else
                {
                    //verify that the email is not already in use by another user
                    LinkUserModel existingUserByEmail = await queryUserByEmail.Execute(model.Email, context.RequestAborted);
                    if (existingUserByEmail != null && existingUserByEmail.Id.ToString() != id.ToString())
                    {
                        return Results.Conflict("A user with the same email already exists.");
                    }
                }

                //update an existing user
                var updateResult = await command.Execute(requestor, model, context.RequestAborted);
                if (!updateResult)
                {
                    return Results.Problem("Failed to update user");
                }

                logger.LogUpdateUser(id.ToString(), context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.NoContent();
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
