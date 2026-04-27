using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Services.SecretManager;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Extensions.Security
{
    public static class SecretManagerExtension
    {
        public static IServiceCollection AddSecretManager(this IServiceCollection services, Action<SecretManagerOptions> options)
        {
            var secretManagerOptions = new SecretManagerOptions();
            options(secretManagerOptions);

            switch (secretManagerOptions.Manager)
            {
                case "Local":
                    services.AddSingleton<ISecretManager, LocalSecretManager>();
                    break;
                case "AzureKeyVault":
                    services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();
                    break;

                default:
                    services.AddSingleton<ISecretManager, LocalSecretManager>();
                    break;
            }

            return services;
        }

        public class SecretManagerOptions
        {
            public string Manager { get; set; } = "Local";
        }
    }
}
