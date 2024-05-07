using LantanaGroup.Link.Account.Application.Commands.Role.UpdateClaims;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class UpdateRoleClaims
    {
        public static async Task<IResult> Handle(HttpContext context, string id, RoleClaimsModel model,
            ILogger logger, IRoleRepository roleRepository, ILinkRoleModelFactory linkRoleModelFactory, IUpdateClaims command)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Results.BadRequest("A role id is required");
            }

            //check if role exists
            var role = await roleRepository.GetRoleAsync(id, cancellationToken: context.RequestAborted);
            if (role is null)
            {
                return Results.NotFound();
            }

            var requestor = context.User;
            var outcome = await command.Execute(requestor, id, model.Claims, context.RequestAborted);
            if (!outcome)
            {
                return Results.Problem("Failed to update role claims");
            }

            var roleModel = linkRoleModelFactory.Create(role);
            roleModel.Claims = model.Claims;

            logger.LogRoleUpdated(roleModel.Name, requestor.Claims.First(c => c.Type == "sub").Value ?? "Uknown", roleModel);

            return Results.NoContent();
        }
    }
}
