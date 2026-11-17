using System.Net.Http.Headers;
using System.Security.Claims;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;

public class ValidationService
{
    //TODO: register once service registry has been updated to include validation service
    private readonly ILogger<ValidationService> _logger;
    private readonly HttpClient _client;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<AuthenticationSchemaConfig> _authenticationSchemaConfig;
    private readonly IServiceScopeFactory _scopeFactory;

    public ValidationService(ILogger<ValidationService> logger,
                             HttpClient client,
                             IOptions<ServiceRegistry> serviceRegistry,
                             IOptions<AuthenticationSchemaConfig> authenticationSchemaConfig,
                             IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _authenticationSchemaConfig = authenticationSchemaConfig ??
            throw new ArgumentNullException(nameof(authenticationSchemaConfig));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

        InitHttpClient();
    }

    private void InitHttpClient()
    {
        //check if the service uri is set
        if (string.IsNullOrEmpty(_serviceRegistry.Value.ValidationServiceUrl))
        {
            _logger.LogGatewayServiceUriException("ValidationService", "Validation service uri is not set");
            throw new ArgumentNullException("Validation Service URL is missing.");
        }

        _client.BaseAddress = new Uri(_serviceRegistry.Value.ValidationServiceUrl);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken) =>
        SpringServiceHealth.ReadAsync(_client, "Validation", SpringServiceHealth.Deadline, _logger, cancellationToken);

    /// <summary>
    /// Gets a report's stored validation counts per category, optionally only the categories with the given submit
    /// flag. Validation returns 404 when the report has no stored counts.
    /// </summary>
    public async Task<HttpResponseMessage> GetReportCategoryCounts(ClaimsPrincipal user,
                                                                   string facilityId,
                                                                   string reportId,
                                                                   bool? submit,
                                                                   CancellationToken cancellationToken)
    {
        var relativeUrl = $"api/validation/facilities/{Uri.EscapeDataString(facilityId)}" +
                          $"/reports/{Uri.EscapeDataString(reportId)}/category-counts";
        if (submit.HasValue)
        {
            relativeUrl += $"?submit={(submit.Value ? "true" : "false")}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        await SetAuthHeaderAsync(user, request, cancellationToken);
        return await _client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets every configured validation category.
    /// </summary>
    public async Task<HttpResponseMessage> GetCategories(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/validation/category");
        await SetAuthHeaderAsync(user, request, cancellationToken);
        return await _client.SendAsync(request, cancellationToken);
    }

    private async Task SetAuthHeaderAsync(ClaimsPrincipal user,
                                          HttpRequestMessage request,
                                          CancellationToken cancellationToken)
    {
        if (_authenticationSchemaConfig.Value.EnableAnonymousAccess)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var createLinkBearerToken = scope.ServiceProvider.GetRequiredService<ICreateLinkBearerToken>();
        var token = await createLinkBearerToken.ExecuteAsync(user, 2, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
