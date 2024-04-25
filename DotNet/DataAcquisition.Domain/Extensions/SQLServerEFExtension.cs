using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace DataAcquisition.Domain.Extensions;
public static class SQLServerEFExtension
{
    public static void AddSQLServerEF_DataAcq(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<DataAcquisitionDbContext>((sp, options) =>
        {

            var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

            switch (builder.Configuration.GetValue<string>(DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider))
            {
                case "SqlServer":
                    options.UseSqlServer(
                        builder.Configuration.GetConnectionString(DataAcquisitionConstants.AppSettingsSectionNames.DatabaseConnection))
                       .AddInterceptors(updateBaseEntityInterceptor);
                    break;
                default:
                    throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider}");
            }
        });
    }
}
