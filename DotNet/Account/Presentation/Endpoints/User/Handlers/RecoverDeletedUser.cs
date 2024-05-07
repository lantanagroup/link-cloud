using LantanaGroup.Link.Account.Application.Commands.User;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class RecoverDeletedUser
    {
        public static async Task<IResult> Handle(HttpContext context, string id, 
            ILogger logger, IUserRepository userRepository, IRecoverUser command)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Results.BadRequest("A user id is required");
            }

            var requestor = context.User;

            var user = await userRepository.GetUserAsync(id, cancellationToken: context.RequestAborted);
            if (user is null)
            {
                return Results.NotFound();
            }

            var outcome = await command.Execute(requestor, id, context.RequestAborted);
            if (outcome is null)
            {
                return Results.Problem("Failed to recover deleted user");
            }

            logger.LogUserRecovery(id, requestor.Claims.First(c => c.Type == "sub").Value ?? "Uknown");

            return Results.Ok(outcome);
        }
    }
}
