using System.Net;
using System.Security.Claims;
using Claim = System.Security.Claims.Claim;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AdminBFF;

/// <summary>
/// Pins what Admin BFF answers an API call that is not signed in, or signed in without the permission it needs:
/// 401 and 403, as the API guidance asks.
/// </summary>
/// <remarks>
/// The status comes from Link's own bearer scheme, which every AuthenticatedUser policy includes and which is
/// asked last. The cookie scheme is asked first and leaves its redirect target in the Location header, so these
/// tests check the status only.
/// </remarks>
[Trait("Category", "IntegrationTests")]
public class AdminBffAccessResponseTests
{
    public static TheoryData<string, bool> Configurations => new()
    {
        { "link_oauth2", true },
        { "link_cookie", false }
    };

    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task Get_BffEndpointWithoutSignIn_ReturnsUnauthorized(string challengeScheme, bool oauth2Enabled)
    {
        using var factory = new AdminBffApiFactory(challengeScheme, oauth2Enabled);
        using var client = factory.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/user");

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task Get_ProxiedRouteWithoutSignIn_ReturnsUnauthorized(string challengeScheme, bool oauth2Enabled)
    {
        using var factory = new AdminBffApiFactory(challengeScheme, oauth2Enabled);
        using var client = factory.CreateNonRedirectingClient();

        using var response = await client.GetAsync("/api/account/accounts");

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task Get_AdminEndpointSignedInWithoutPermission_ReturnsForbidden(string challengeScheme,
                                                                                   bool oauth2Enabled)
    {
        using var factory = new AdminBffApiFactory(challengeScheme, oauth2Enabled);
        using var client = factory.CreateNonRedirectingClient();
        var cookie = factory.CreateSignedInCookie(new Claim("sub", "user-1"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/refresh-key");
        request.Headers.Add("Cookie", cookie);

        using var response = await client.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(expected == response.StatusCode,
                    $"Expected {(int)expected} but got {(int)response.StatusCode}; " +
                    $"Location: {response.Headers.Location?.ToString() ?? "(none)"}; " +
                    $"WWW-Authenticate: {string.Join(" | ", response.Headers.WwwAuthenticate)}; " +
                    $"Body: {body}");
    }
}
