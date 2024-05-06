using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class GetUserByEmail
    {
        public static async Task<IResult> Handle(HttpContext context, string email, ILogger logger, IGetUserByEmail getUserCommand)
        {
            if (string.IsNullOrEmpty(email))
            {
                return Results.BadRequest("A user email address is required");
            }

            var user = await getUserCommand.Execute(email, context.RequestAborted);
            if (user is null)
            {
                return Results.NotFound();
            }

            logger.LogFindUser(email, context.User.Claims.First(c => c.Type == "sub").Value ?? "Uknown");

            return Results.Ok(user);
        }
    }
}
