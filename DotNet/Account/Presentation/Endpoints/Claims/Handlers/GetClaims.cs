using LantanaGroup.Link.Account.Application.Models;
using Link.Authorization.Permissions;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Claims.Handlers
{
    public static class GetClaims
    {
        public static async Task<IResult> Handle(HttpContext context, [FromServices] ILogger<ClaimsEndpoints> logger)
        {
            try
            {
                LinkClaimsModel claims = new()
                {
                    Claims = LinkPermissionsProvider.GetLinkPermissions().ConvertAll(x => x.ToString())
                };

                return Results.Ok(claims);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                logger.LogError(ex, "Error getting claims");
                throw;
            }
            
        }
    }
}
