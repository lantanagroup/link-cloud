using LantanaGroup.Link.Account.Application.Queries.Role.GetRoleByName;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class GetRoleByName
    {
        public static async Task<IResult> Handle(HttpContext context, string name, 
            ILogger logger, IGetRoleByName query)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Results.BadRequest("A role name is required");
            }

            var requestor = context.User;
            var role = await query.Execute(name, context.RequestAborted);

            if (role is null)
            {
                return Results.NotFound();
            }

            logger.LogFindRole(name, requestor.Claims.First(c => c.Type == "sub").Value ?? "Uknown", role);

            return Results.Ok(role);
        }
    }
}
