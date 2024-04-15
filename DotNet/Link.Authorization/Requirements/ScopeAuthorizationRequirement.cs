using Microsoft.AspNetCore.Authorization;

namespace Link.Authorization.Requirements
{
    public class ScopeAuthorizationRequirement : AuthorizationHandler<ScopeAuthorizationRequirement>, IAuthorizationRequirement
    {
        public IEnumerable<string> RequiredScopes { get; }

        public ScopeAuthorizationRequirement(IEnumerable<string> requiredScopes)
        {
            if (requiredScopes == null || !requiredScopes.Any())
            {
                throw new ArgumentException($"{nameof(requiredScopes)} must contain at least one value.", nameof(requiredScopes));
            }

            RequiredScopes = requiredScopes;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeAuthorizationRequirement requirement)
        {
            if (context.User != null)
            {
                var scopeClaim = context.User.Claims.FirstOrDefault(
                    c => string.Equals(c.Type, "scope", StringComparison.OrdinalIgnoreCase));

                if (scopeClaim != null)
                {
                    var scopes = scopeClaim.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (requirement.RequiredScopes.All(requiredScope => scopes.Contains(requiredScope)))
                    {
                        context.Succeed(requirement);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
