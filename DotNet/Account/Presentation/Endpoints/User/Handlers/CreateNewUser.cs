using LantanaGroup.Link.Account.Application.Commands.User;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class CreateNewUser
    {
        public static async Task<IResult> Handle(HttpContext context, 
            LinkUserModel model, [FromServices] ILogger<UserEndpoints> logger, [FromServices] ICreateUser command)
        {
            try
            {
                if (model is null)
                {
                    return Results.BadRequest("No user was provided in the request.");
                }

                var requestor = context.User;
                var createdUser = await command.Execute(requestor, model, context.RequestAborted);

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
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogUserCreationException(ex.Message);
                throw;
            }            
        }        
    }
}
