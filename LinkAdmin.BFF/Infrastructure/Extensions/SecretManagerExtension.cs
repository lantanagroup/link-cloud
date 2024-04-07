using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.SecretManagers;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class SecretManagerExtension
    {
        public static IServiceCollection AddSecretManager(this IServiceCollection services, Action<SecretManagerOptions> options)
        {
            var secretManagerOptions = new SecretManagerOptions();
            options(secretManagerOptions);

            switch(secretManagerOptions.Manager)
            {
                case "AzureKeyVault":
                    services.AddSingleton<ISecretManager, LinkAzureKeyVault>();
                    break;

                default:
                    throw new ArgumentException("Invalid secret manager");
            }
            services.AddSingleton<ISecretManager, LinkAzureKeyVault>();

            return services;
        }

        public class SecretManagerOptions
        {
            public string Manager { get; set; } = null!;
        }
    }
}
