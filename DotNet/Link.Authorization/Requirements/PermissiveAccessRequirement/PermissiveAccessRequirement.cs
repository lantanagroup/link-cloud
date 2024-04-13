using Microsoft.AspNetCore.Authorization;

namespace Link.Authorization.Requirements.PermissiveAccessRequirement
{
    public class PermissiveAccessRequirement : IAuthorizationRequirement
    {
    }

    public class PermissiveAccessRequirementHandler : AuthorizationHandler<PermissiveAccessRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissiveAccessRequirement requirement)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}
