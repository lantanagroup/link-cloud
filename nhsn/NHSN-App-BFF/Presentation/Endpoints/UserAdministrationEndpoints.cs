using System.Security.Claims;
using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class UserAdministrationEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/users")
            .WithTags("NHSN App BFF")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet(string.Empty, async (IUserAdministrationService userAdministrationService, CancellationToken cancellationToken) =>
            {
                var users = await userAdministrationService.GetUsersAsync(cancellationToken);
                return users.Count == 0 ? Results.NoContent() : Results.Ok(users);
            })
            .WithName("GetNhsnUsers")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{userId:guid}/roles", async (Guid userId, UpdateUserRolesRequest request, HttpContext httpContext, IUserAdministrationService userAdministrationService, IOptions<NhsnJwtSettings> jwtOptions, CancellationToken cancellationToken) =>
            {
                try
                {
                    var actingExternalUserId = ResolveActingExternalUserId(httpContext, jwtOptions.Value);
                    var updated = await userAdministrationService.UpdateUserRolesAsync(userId, actingExternalUserId, request, cancellationToken);
                    return updated is null ? Results.NotFound() : Results.Ok(updated);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .WithName("UpdateNhsnUserRoles")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{userId:guid}/status", async (Guid userId, UpdateUserStatusRequest request, HttpContext httpContext, IUserAdministrationService userAdministrationService, IOptions<NhsnJwtSettings> jwtOptions, CancellationToken cancellationToken) =>
            {
                try
                {
                    var actingExternalUserId = ResolveActingExternalUserId(httpContext, jwtOptions.Value);
                    var updated = await userAdministrationService.UpdateUserStatusAsync(userId, actingExternalUserId, request, cancellationToken);
                    return updated is null ? Results.NotFound() : Results.Ok(updated);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .WithName("UpdateNhsnUserStatus")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static string ResolveActingExternalUserId(HttpContext httpContext, NhsnJwtSettings settings)
    {
        if (settings.AllowSimulatedJwtHeader && httpContext.Request.Headers.TryGetValue(settings.SimulatedJwtHeaderName, out var headerValue))
        {
            var payload = JsonSerializer.Deserialize<SimulatedUserHeaderPayload>(headerValue.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (payload is not null && !string.IsNullOrWhiteSpace(payload.ExternalUserId ?? payload.Email))
            {
                return payload.ExternalUserId ?? payload.Email;
            }
        }

        return httpContext.User.FindFirstValue(settings.UserIdClaimType)
               ?? httpContext.User.FindFirstValue(settings.EmailClaimType)
               ?? httpContext.User.Identity?.Name
               ?? throw new InvalidOperationException("Unable to resolve the acting user.");
    }
}