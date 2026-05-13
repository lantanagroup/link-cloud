using Microsoft.Extensions.Configuration;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddStandardEnvironmentConfiguration(this IConfigurationBuilder builder)
    {
        // 1. Load the .env file from the project root or parent directories
        DotNetEnv.Env.TraversePath().Load();

        // 2. Ensure Environment Variables are included
        builder.AddEnvironmentVariables();

        // 3. Enable the ${VAR} substitution logic
        return builder.AddSubstitution();
    }
}