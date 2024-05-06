using LantanaGroup.Link.Account.Application.Commands.User;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class CreateNewUser
    {
        public static async Task<IResult> Handle(HttpContext context, 
            LinkUserModel model, ILogger logger, ICreateUser createUserCommand)
        {            
            if (model is null)
            {
                return Results.BadRequest("No user was provided in the request.");
            }

            var requestor = context.User;
            var createdUser = await createUserCommand.Execute(requestor, model, context.RequestAborted);

            logger.LogUserCreated(createdUser.Id, requestor.Claims.First(c => c.Type == "sub").Value ?? "Uknown");

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

        
    }
}
