using LantanaGroup.Link.Account.Application.Commands.Role.DeleteRole;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Infrastructure.Logging;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class DeleteExistingRole
    {
        public static async Task<IResult> Handle(HttpContext context, string id, 
            ILogger logger, IRoleRepository roleRepository, ILinkRoleModelFactory linkRoleModelFactory, IDeleteRole command)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Results.BadRequest("A role id is required");
            }
            
            var requestor = context.User;

            //check if role exists
            var role = await roleRepository.GetRoleAsync(id, cancellationToken: context.RequestAborted);
            if(role is null)
            {
                return Results.NotFound();
            }

            //delete role
            var outcome = await command.Execute(requestor, id, cancellationToken: context.RequestAborted);
            if (!outcome)
            {
                return Results.Problem("Failed to delete role");
            }

            var roleMOdel = linkRoleModelFactory.Create(role);

            logger.LogRoleDeleted(roleMOdel.Name, requestor.Claims.First(c => c.Type == "sub").Value ?? "Uknown", roleMOdel);

            return Results.Ok();
        }
    }
}
