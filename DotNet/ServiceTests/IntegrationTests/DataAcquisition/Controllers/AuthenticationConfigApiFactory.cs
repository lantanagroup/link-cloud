using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace IntegrationTests.DataAcquisition.Controllers;

/// <summary>
/// Hosts <see cref="AuthenticationConfigController"/> through a real MVC pipeline with mocked
/// dependencies, so model binding and the [ApiController] validation filter run.
/// </summary>
public sealed class AuthenticationConfigApiFactory : WebApplicationFactory<AuthenticationConfigApiFactory.ApiTestMarker>
{
    /// <summary>
    /// The query configuration manager the controller writes through.
    /// </summary>
    public Mock<IFhirQueryConfigurationManager> QueryConfigurationManager { get; } = new();

    /// <summary>
    /// The query configuration queries the controller reads through.
    /// </summary>
    public Mock<IFhirQueryConfigurationQueries> QueryConfigurationQueries { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services
                            .AddControllers(options =>
                                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
                            .AddApplicationPart(typeof(AuthenticationConfigController).Assembly);

                        services.AddSingleton(QueryConfigurationManager.Object);
                        services.AddSingleton(QueryConfigurationQueries.Object);

                        services
                            .AddAuthentication(TestAuthHandler.SchemeName)
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                TestAuthHandler.SchemeName, _ => { });
                        services.AddAuthorization(options =>
                            options.AddPolicy(PolicyNames.IsLinkAdmin, p => p.RequireAuthenticatedUser()));
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            });
    }

    /// <summary>
    /// Marker type used only to locate the test assembly and content root.
    /// </summary>
    public sealed class ApiTestMarker
    {
    }
}
