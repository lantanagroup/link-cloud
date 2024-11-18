using LantanaGroup.Link.Account.Application.Queries.Role;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class GetRoleById
    {
        public static async Task<IResult> Handle(HttpContext context, Guid id, 
            [FromServices] ILogger<RoleEndpoints> logger, [FromServices] IGetRole query)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return Results.BadRequest("A role id is required");
                }

                var requestor = context.User;
                var role = await query.Execute(id, context.RequestAborted);
                if (role is null)
                {
                    return Results.NotFound();
                }

                logger.LogFindRole(role.Name, requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown", role);

                return Results.Ok(role);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogFindRoleException(id.ToString(), ex.Message);
                throw;
            }
            
        }
    }
}
