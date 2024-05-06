using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class GetUser
    {
        public static async Task<IResult> Handle(HttpContext context, string id, ILogger logger, IGetUserByid getUserCommand)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Results.BadRequest("A user id is required");
            }

            var user = await getUserCommand.Execute(id, context.RequestAborted);
            if (user is null)
            {
                return Results.NotFound();
            }

            logger.LogFindUser(id, context.User.Claims.First(c => c.Type == "sub").Value ?? "Uknown");

            return Results.Ok(user);
        }
    }
}
