using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class UserInfoEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff")
            .WithTags("NHSN App BFF")
            .WithOpenApi();

        group.MapGet("/userinfo", async (HttpContext context, IUserInfoService userInfoService, CancellationToken cancellationToken) =>
            {
                var response = await userInfoService.GetUserInfoAsync(context.User, context.Request, cancellationToken);
                return Results.Ok(response);
            })
            .RequireAuthorization("AuthenticatedUser")
            .WithName("GetUserInfo")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get the resolved NHSNLink user context.";
                operation.Description = "Returns the authenticated NHSN App user context including FACADMIN-derived access state, facility context, and onboarding state.";
                return operation;
            });
    }
}