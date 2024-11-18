using LantanaGroup.Link.Account.Application.Commands.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class DeleteExistingRole
    {
        public static async Task<IResult> Handle(HttpContext context, Guid id,
            [FromServices] ILogger<RoleEndpoints> logger, [FromServices] IRoleRepository roleRepository, [FromServices] ILinkRoleModelFactory linkRoleModelFactory, [FromServices] IDeleteRole command)
        {

            try
            {
                if (id == Guid.Empty)
                {
                    return Results.BadRequest("A role id is required");
                }

                var requestor = context.User;

                //check if role exists
                var role = await roleRepository.GetRoleAsync(id, cancellationToken: context.RequestAborted);
                if (role is null)
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

                logger.LogRoleDeleted(roleMOdel.Name, requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown", roleMOdel);

                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogRoleDeletionException(id.ToString(), ex.Message);
                throw;
            }            
        }
    }
}
