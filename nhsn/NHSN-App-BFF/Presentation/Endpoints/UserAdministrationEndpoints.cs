using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

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

        group.MapPut("/{userId:guid}/roles", async (Guid userId, UpdateUserRolesRequest request, IUserAdministrationService userAdministrationService, CancellationToken cancellationToken) =>
            {
                var updated = await userAdministrationService.UpdateUserRolesAsync(userId, request, cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            })
            .WithName("UpdateNhsnUserRoles")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}