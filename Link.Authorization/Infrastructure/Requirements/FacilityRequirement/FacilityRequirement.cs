using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace Link.Authorization.Infrastructure.Requirements
{
    public class FacilityRequirement : IAuthorizationRequirement
    {
    }

    public class FacilityRequirementHandler : AuthorizationHandler<FacilityRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FacilityRequirement requirement)
        {
            if (!context.User.HasClaim(c => c.Type == "facilities"))
            {
                return Task.CompletedTask;
            }

            var facilityIdClaim = context.User.FindAll(c => c.Type == "facilities").ToList();

            var routeData = context.Resource as RouteData;
            if (routeData == null)
            {
                return Task.CompletedTask;
            }

            var requestFacilityId = routeData.Values["facilityId"] as string;

            if (facilityIdClaim.Any(x => x.Value.Equals(requestFacilityId, StringComparison.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
