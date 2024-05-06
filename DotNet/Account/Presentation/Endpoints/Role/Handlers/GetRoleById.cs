using LantanaGroup.Link.Account.Application.Queries.Role.GetRole;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class GetRoleById
    {
        public static async Task<IResult> Handle(HttpContext context, string id, 
            ILogger logger, IGetRole command)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Results.BadRequest("A role id is required");
            }

            var requestor = context.User;
            var role = await command.Execute(id, context.RequestAborted);
            if (role is null)
            {
                return Results.NotFound();
            }

            logger.LogFindRole(role.Name, requestor.Claims.First(c => c.Type == "sub").Value ?? "Uknown", role);

            return Results.Ok(role);
        }
    }
}
