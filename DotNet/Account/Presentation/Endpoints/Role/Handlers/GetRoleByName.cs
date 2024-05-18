using LantanaGroup.Link.Account.Application.Queries.Role;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers
{
    public static class GetRoleByName
    {
        public static async Task<IResult> Handle(HttpContext context, string name, 
            [FromServices] ILogger<RoleEndpoints> logger, [FromServices] IGetRoleByName query)
        {
            try
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

                logger.LogFindRole(name, requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown", role);

                return Results.Ok(role);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogFindRoleException(name, ex.Message);
                throw;
            }            
        }
    }
}
