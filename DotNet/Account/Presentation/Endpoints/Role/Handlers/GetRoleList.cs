using LantanaGroup.Link.Account.Application.Queries.Role;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class GetRoleList
    {
        public static async Task<IResult> Handle(HttpContext context, [FromServices] ILogger<RoleEndpoints> logger, [FromServices] IGetRoles query)
        {
            try
            {
                var roles = await query.Execute(context.RequestAborted);

                logger.LogFindRoles(context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown");

                return Results.Ok(roles);
            }
            catch(Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogFindRolesException(ex.Message);
                throw;
            }
            
        }

    }
}
