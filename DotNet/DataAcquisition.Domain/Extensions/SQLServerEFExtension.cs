﻿using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;

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
                case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                    string? connectionString =
                        builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections
                            .DatabaseConnection);

                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Database connection string is null or empty.");
                    
                    options.UseSqlServer(connectionString)
                       .AddInterceptors(updateBaseEntityInterceptor);
                    break;
                default:
                    throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider}");
            }
        });
    }
}
