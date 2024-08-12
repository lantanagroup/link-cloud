using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.ExternalServices
{
    public static class LinkClientExtension
    {
        public static IServiceCollection AddLinkClients(this IServiceCollection services)
        {
            services.AddHttpClient<AccountService>();

            return services;
        }
    }
}
