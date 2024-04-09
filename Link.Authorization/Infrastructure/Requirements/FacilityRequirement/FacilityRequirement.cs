using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace Link.Authorization.Infrastructure.Requirements
{
    public class FacilityRequirement : IAuthorizationRequirement
    {
    }

    public class FacilityRequirementHandler : AuthorizationHandler<FacilityRequirement, string>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FacilityRequirement requirement, string facilityId)
        {
            if (!context.User.HasClaim(c => c.Type == "facilities"))
            {
                return Task.CompletedTask;
            }

            var facilityIdClaim = context.User.FindAll(c => c.Type == "facilities").ToList();           

            if (facilityIdClaim.Any(x => x.Value.Equals(facilityId, StringComparison.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
