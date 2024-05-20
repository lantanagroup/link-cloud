using LantanaGroup.Link.Account.Application.Commands.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class CreateNewRole
    {
        public static async Task<IResult> Handle(HttpContext context, LinkRoleModel model,
            [FromServices]ILogger<RoleEndpoints> logger, [FromServices] IRoleRepository roleRepository, [FromServices] ICreateRole command)
        {
            try
            {
                var requestor = context.User;

                //check if the role already exists
                var existingRole = await roleRepository.GetRoleByNameAsync(model.Name, cancellationToken: context.RequestAborted);
                if (existingRole is not null)
                {
                    return Results.BadRequest("Role already exists");
                }

                var role = await command.Execute(requestor, model, context.RequestAborted);
                if (role is null)
                {
                    return Results.Problem("Role creation failed");
                }

                logger.LogRoleCreated(role.Name, requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown", role);

                return Results.Ok(role);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                throw;
            }
            
        }
    }
}
