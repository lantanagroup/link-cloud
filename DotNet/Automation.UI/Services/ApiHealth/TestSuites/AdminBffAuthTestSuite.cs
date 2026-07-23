using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.AdminBffAuthSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Executes Admin BFF authentication-only checks against a protected endpoint.
/// </summary>
public sealed class AdminBffAuthTestSuite : ServiceTestSuiteBase
{
    private const string ProtectedPath = "/aggregate/reports/summaries";
    private const int MinHs512SigningKeyBytes = 64;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<LinkTokenServiceSettings> _tokenSettings;
    private readonly ICreateSystemToken _createSystemToken;

    public override string ServiceName => ApiEndPointLibrary.ServiceNames.AdminBffAuth;

    public AdminBffAuthTestSuite(
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<LinkTokenServiceSettings> tokenSettings,
        ICreateSystemToken createSystemToken)
    {
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
        _tokenSettings = tokenSettings;
        _createSystemToken = createSystemToken;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var baseUrl = _serviceRegistry.Value.AdminBffServiceApiUrl?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            const string missingAdminBffConfigError = "ServiceRegistry:AdminBffServiceApiUrl is not configured.";
            results.Add(MakeFailedResult(StepNames.MissingAuthHeaderGet401, missingAdminBffConfigError));

            var remainingSteps = new[]
            {
                StepNames.ValidBearerGet200,
                StepNames.TokenReuseGet200,
                StepNames.InvalidSignatureBearerGet401,
                StepNames.ExpiredBearerGet401Or403,
                StepNames.EmptyBearerGet401,
                StepNames.MalformedBearerGet401,
                StepNames.InvalidAuthSchemeGet401,
                StepNames.CrossApiTokenReuseGet401
            };

            foreach (var step in remainingSteps)
                results.Add(SkipStepAsync(step, missingAdminBffConfigError));

            return results;
        }

        var endpointUrl = $"{baseUrl}{ProtectedPath}";
        var canGenerateTokens = TryGetTokenGenerationInputs(out var signingKey, out var authority, out var tokenConfigError);

        var tokenScenarioSteps = new[]
        {
            StepNames.ValidBearerGet200,
            StepNames.TokenReuseGet200,
            StepNames.InvalidSignatureBearerGet401,
            StepNames.ExpiredBearerGet401Or403,
            StepNames.CrossApiTokenReuseGet401
        };
        var completedTokenScenarioSteps = new HashSet<string>(StringComparer.Ordinal);

        string? validToken = null;
        if (canGenerateTokens)
        {
            try
            {
                validToken = await _createSystemToken.ExecuteAsync(signingKey!, 5);
                var validTokenResult = await CallEndpointAsync(StepNames.ValidBearerGet200, endpointUrl, 200, Header("Bearer", validToken), false, ct);
                results.Add(validTokenResult);
                completedTokenScenarioSteps.Add(StepNames.ValidBearerGet200);

                var tokenReuseResult = await CallEndpointAsync(StepNames.TokenReuseGet200, endpointUrl, 200, Header("Bearer", validToken), false, ct);
                results.Add(tokenReuseResult);
                completedTokenScenarioSteps.Add(StepNames.TokenReuseGet200);

                var invalidSignatureToken = BuildToken(
                    CreateSigningKeyForHs512(),
                    authority!,
                    LinkAuthorizationConstants.LinkBearerService.LinkBearerAudience,
                    DateTime.UtcNow.AddMinutes(5));
                var invalidSignatureResult = await CallEndpointAsync(StepNames.InvalidSignatureBearerGet401, endpointUrl, 401, Header("Bearer", invalidSignatureToken), true, ct);
                results.Add(invalidSignatureResult);
                completedTokenScenarioSteps.Add(StepNames.InvalidSignatureBearerGet401);

                var expiredToken = await _createSystemToken.ExecuteAsync(signingKey!, -5);
                var expiredTokenResult = await CallEndpointAsync(StepNames.ExpiredBearerGet401Or403, endpointUrl, [401, 403], Header("Bearer", expiredToken), true, ct);
                results.Add(expiredTokenResult);
                completedTokenScenarioSteps.Add(StepNames.ExpiredBearerGet401Or403);

                var wrongAudienceToken = BuildToken(
                    signingKey!,
                    authority!,
                    "LinkServices-OtherApi",
                    DateTime.UtcNow.AddMinutes(5));
                var crossApiTokenReuseResult = await CallEndpointAsync(StepNames.CrossApiTokenReuseGet401, endpointUrl, 401, Header("Bearer", wrongAudienceToken), true, ct);
                results.Add(crossApiTokenReuseResult);
                completedTokenScenarioSteps.Add(StepNames.CrossApiTokenReuseGet401);
            }
            catch (Exception ex)
            {
                var error = $"Token generation failed for auth test scenarios: {ex.Message}";
                foreach (var step in tokenScenarioSteps.Where(step => !completedTokenScenarioSteps.Contains(step)))
                    results.Add(SkipStepAsync(step, error));
            }
        }
        else
        {
            results.Add(SkipStepAsync(StepNames.ValidBearerGet200, tokenConfigError!));
            results.Add(SkipStepAsync(StepNames.TokenReuseGet200, tokenConfigError!));
            results.Add(SkipStepAsync(StepNames.InvalidSignatureBearerGet401, tokenConfigError!));
            results.Add(SkipStepAsync(StepNames.ExpiredBearerGet401Or403, tokenConfigError!));
            results.Add(SkipStepAsync(StepNames.CrossApiTokenReuseGet401, tokenConfigError!));
        }

        results.Add(await CallEndpointAsync(StepNames.EmptyBearerGet401, endpointUrl, 401, Header("Bearer", string.Empty), true, ct));
        results.Add(await CallEndpointAsync(StepNames.MalformedBearerGet401, endpointUrl, 401, Header("Bearer", "malformed.token.value"), true, ct));
        results.Add(await CallEndpointAsync(StepNames.MissingAuthHeaderGet401, endpointUrl, 401, null, true, ct));

        var basicCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes("api-health:invalid"));
        results.Add(await CallEndpointAsync(StepNames.InvalidAuthSchemeGet401, endpointUrl, 401, Header("Basic", basicCredential), true, ct));

        return results;
    }

    private async Task<ApiTestRunResult> CallEndpointAsync(
        string endpointName,
        string requestUrl,
        int expectedStatusCode,
        AuthenticationHeaderValue? authorization,
        bool validateAuthErrorMessage,
        CancellationToken ct)
    {
        return await CallEndpointAsync(endpointName, requestUrl, [expectedStatusCode], authorization, validateAuthErrorMessage, ct);
    }

    private async Task<ApiTestRunResult> CallEndpointAsync(
        string endpointName,
        string requestUrl,
        int[] expectedStatusCodes,
        AuthenticationHeaderValue? authorization,
        bool validateAuthErrorMessage,
        CancellationToken ct)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = expectedStatusCodes[0],
            ExecutedAt = DateTimeOffset.UtcNow,
            RequestMethod = HttpMethod.Get.Method,
            RequestUrl = requestUrl,
            RequestBody = "No request body was sent (GET)."
        };

        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (authorization is not null)
                request.Headers.Authorization = authorization;

            var authHeaderValue = request.Headers.Authorization?.ToString();
            if (string.IsNullOrWhiteSpace(authHeaderValue))
                result.RequestBody = "No request body was sent (GET). Authorization header: <missing>.";
            else
                result.RequestBody = $"No request body was sent (GET). Authorization header: {authHeaderValue}.";

            var client = _httpClientFactory.CreateClient("ApiHealthTest");
            using var response = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.ActualStatusCode = (int)response.StatusCode;
            var responseCodeMatchesExpected = expectedStatusCodes.Contains(result.ActualStatusCode.Value);
            result.ExpectedStatusCode = responseCodeMatchesExpected
                ? result.ActualStatusCode.Value
                : expectedStatusCodes[0];
            result.Passed = responseCodeMatchesExpected;
            result.ResponseBody = string.IsNullOrWhiteSpace(responseBody)
                ? $"No response body was returned (HTTP {(int)response.StatusCode})."
                : (responseBody.Length > 500 ? responseBody[..500] : responseBody);
            result.TraceId = ExtractTraceId(response);

            if (!responseCodeMatchesExpected && validateAuthErrorMessage && !LooksLikeAuthError(result.ResponseBody))
            {
                result.ErrorMessage = "Denied response did not include an authentication/authorization error message.";
            }

            if (!result.Passed && string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                result.ErrorMessage = $"Expected HTTP {string.Join("/", expectedStatusCodes)} but got {result.ActualStatusCode}.";
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = ex.Message;
            result.ResponseBody = ex.ToString();
        }

        return result;
    }

    private bool TryGetTokenGenerationInputs(out string? signingKey, out string? authority, out string? error)
    {
        signingKey = _tokenSettings.Value.SigningKey;
        authority = _tokenSettings.Value.Authority;

        if (string.IsNullOrWhiteSpace(signingKey) || string.IsNullOrWhiteSpace(authority))
        {
            error = "LinkTokenService:SigningKey and LinkTokenService:Authority are required to generate auth-test tokens.";
            return false;
        }

        var keyBytes = Encoding.UTF8.GetByteCount(signingKey);
        if (keyBytes < MinHs512SigningKeyBytes)
        {
            error = $"LinkTokenService:SigningKey is too short for HS512 ({keyBytes * 8} bits). Minimum is {MinHs512SigningKeyBytes * 8} bits.";
            return false;
        }

        error = null;
        return true;
    }

    private static string CreateSigningKeyForHs512()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(MinHs512SigningKeyBytes);
        return Convert.ToBase64String(keyBytes);
    }

    private static AuthenticationHeaderValue Header(string scheme, string parameter) => new(scheme, parameter);

    private static string BuildToken(string signingKey, string issuer, string audience, DateTime expiresUtc)
    {
        var claims = new List<Claim>
        {
            new(LinkAuthorizationConstants.LinkSystemClaims.Email, "automation-ui@link.invalid"),
            new(LinkAuthorizationConstants.LinkSystemClaims.Subject, LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount),
            new(LinkAuthorizationConstants.LinkSystemClaims.Role, LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor),
            new(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, "IsLinkAdmin")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha512);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool LooksLikeAuthError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        var normalized = body.Trim();
        return normalized.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("invalid", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractTraceId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("traceparent", out var traceParents))
        {
            var traceParent = traceParents.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(traceParent))
            {
                var parts = traceParent.Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                    return parts[1];
            }
        }

        if (response.Headers.TryGetValues("X-Trace-Id", out var traceIds))
            return traceIds.FirstOrDefault();

        return null;
    }

    private ApiTestRunResult MakeFailedResult(string endpointName, string error) => new()
    {
        EndpointKey = $"{ServiceName}::{endpointName}",
        ServiceName = ServiceName,
        EndpointName = endpointName,
        Passed = false,
        ErrorMessage = error,
        RequestBody = "Request was not sent because required configuration is missing.",
        ResponseBody = "Response was not received because required configuration is missing.",
        ExecutedAt = DateTimeOffset.UtcNow
    };
}
