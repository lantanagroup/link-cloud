using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class EFMigrations
{
    public static void AutoMigrateEF<T>(this WebApplication app) where T : DbContext
    {
        if (app.Configuration.GetValue<bool>(ConfigurationConstants.AppSettings.AutoMigrate, false))
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<T>();
            dbContext.Database.Migrate();
        }
    }
}