using LantanaGroup.Link.Account.Application.Commands.Role.UpdateClaims;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Application.Queries.Role;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class UpdateRoleClaims
    {
        public static async Task<IResult> Handle(HttpContext context, string id, RoleClaimsModel model,
            [FromServices] ILogger<RoleEndpoints> logger, [FromServices] IGetRole queryRole, [FromServices] IUpdateClaims command)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Results.BadRequest("A role id is required");
                }

                //check if role exists
                var role = await queryRole.Execute(id, cancellationToken: context.RequestAborted);
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
                role.Claims = model.Claims;

                logger.LogRoleUpdated(role.Name, requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown", role);

                return Results.Ok(role);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogRoleClaimAssignmentException(id, string.Join(",", model.Claims), ex.Message);
                throw;
            }
            
        }
    }
}
