using System.Security.Claims;
using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Middleware;

public class AdminGroupAugmentationMiddleware
{
    private readonly RequestDelegate _next;

    public AdminGroupAugmentationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, NhsnAppDbContext dbContext, IOptions<NhsnJwtSettings> jwtOptions)
    {
        var settings = jwtOptions.Value;
        var externalUserId = ResolveExternalUserId(context, settings);

        if (!string.IsNullOrWhiteSpace(externalUserId))
        {
            var isAdmin = await dbContext.Users
                .AsNoTracking()
                .Where(x => x.ExternalUserId == externalUserId)
                .Select(x => x.IsAdmin)
                .SingleOrDefaultAsync();

            if (isAdmin)
            {
                var identity = new ClaimsIdentity(context.User.Identity);
                if (!context.User.Claims.Any(x => x.Type == settings.GroupsClaimType && string.Equals(x.Value, NhsnAppConstants.Roles.NhsnLinkSysAdmin, StringComparison.OrdinalIgnoreCase)))
                {
                    identity.AddClaim(new Claim(settings.GroupsClaimType, NhsnAppConstants.Roles.NhsnLinkSysAdmin));
                }

                context.User = new ClaimsPrincipal(identity);
            }
        }

        await _next(context);
    }

    private static string? ResolveExternalUserId(HttpContext context, NhsnJwtSettings settings)
    {
        if (settings.AllowSimulatedJwtHeader && context.Request.Headers.TryGetValue(settings.SimulatedJwtHeaderName, out var headerValue))
        {
            var payload = JsonSerializer.Deserialize<SimulatedUserHeaderPayload>(headerValue.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return payload?.ExternalUserId ?? payload?.Email;
        }

        return context.User.FindFirstValue(settings.UserIdClaimType)
               ?? context.User.FindFirstValue(settings.EmailClaimType)
               ?? context.User.Identity?.Name;
    }
}