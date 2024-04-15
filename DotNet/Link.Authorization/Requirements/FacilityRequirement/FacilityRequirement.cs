using Microsoft.AspNetCore.Authorization;

namespace Link.Authorization.Infrastructure.Requirements
{
    public class FacilityRequirement : IAuthorizationRequirement
    {
    }

    public class FacilityRequirementHandler : AuthorizationHandler<FacilityRequirement, string>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FacilityRequirement requirement, string facilityId)
        {
            //check if user has an admin claim
            if (context.User.HasClaim(c => c.Type == LinkAuthorizationConstants.LinkSystemClaims.Role))
            {
                var isAdmin = context.User.FindAll(c => c.Type == LinkAuthorizationConstants.LinkSystemClaims.Role)
                        .Any(x => x.Value.Equals(LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor, StringComparison.OrdinalIgnoreCase));

                if (isAdmin)
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }                
            }

            //check if user has a facility claim
            if (!context.User.HasClaim(c => c.Type == LinkAuthorizationConstants.LinkSystemClaims.Facility))
            {
                return Task.CompletedTask;
            }

            //check if user is authorized to access the facility
            var facilityIdClaim = context.User.FindAll(c => c.Type == LinkAuthorizationConstants.LinkSystemClaims.Facility).ToList();           

            if (facilityIdClaim.Any(x => x.Value.Equals(facilityId, StringComparison.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
