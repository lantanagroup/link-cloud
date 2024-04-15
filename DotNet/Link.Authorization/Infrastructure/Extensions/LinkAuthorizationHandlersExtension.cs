using Link.Authorization.Infrastructure.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Link.Authorization.Infrastructure.Extensions
{
    public static class LinkAuthorizationHandlersExtension
    {
        public static IServiceCollection AddLinkAuthorizationHandlers(this IServiceCollection services)
        {           
            //register authorization handlers
            services.AddSingleton<IAuthorizationHandler, FacilityRequirementHandler>();

            return services;
        }
    }
}
