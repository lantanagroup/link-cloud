using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class SimulationEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        app.MapGet("/api/nhsn-app-bff/test-users", async (NhsnAppDbContext dbContext, CancellationToken cancellationToken) =>
            {
                var users = await dbContext.Users
                    .AsNoTracking()
                    .OrderBy(x => x.Email)
                    .Select(x => new
                    {
                        x.Id,
                        x.Email,
                        x.Name,
                        x.FacilityId,
                        x.IsOnboarded
                    })
                    .ToListAsync(cancellationToken);

                return users.Count == 0 ? Results.NoContent() : Results.Ok(users);
            })
            .WithTags("NHSN App BFF")
            .RequireAuthorization("AuthenticatedUser");
    }
}