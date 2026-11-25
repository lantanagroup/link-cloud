using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class UserInfoEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff")
            .WithTags("NHSN App BFF");

        group.MapGet("/userinfo", async (IUserInfoService userInfoService, CancellationToken cancellationToken) =>
            {
                var response = await userInfoService.GetUserInfoAsync(cancellationToken);
                return Results.Ok(response);
            })
            .RequireAuthorization("AuthenticatedUser")
            .WithName("GetUserInfo")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Get the resolved NHSNLink user context.")
            .WithDescription("Returns the authenticated NHSN App user context including FACADMIN-derived access state, facility context, and onboarding state.");
    }
}