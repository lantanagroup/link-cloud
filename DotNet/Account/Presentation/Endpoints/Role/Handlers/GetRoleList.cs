using LantanaGroup.Link.Account.Application.Queries.Role.GetRoles;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class GetRoleList
    {
        public static async Task<IResult> Handle(HttpContext context, ILogger logger, IGetRoles query)
        {
            var roles = await query.Execute(context.RequestAborted);

            logger.LogFindRoles(context.User.Claims.First(c => c.Type == "sub").Value ?? "Uknown");

            return Results.Ok(roles);
        }

    }
}
