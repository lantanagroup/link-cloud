using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Session;

/// <summary>
/// LEGLINK-1359: the NHSN caller's email, for the token this BFF mints when it calls a Link service.
/// </summary>
/// <remarks>
/// Deliberately not built on <see cref="Application.Interfaces.Services.INhsnUserContext"/>, though it
/// reads the same claim. That one is scoped and throws when there is no request, which is right for a
/// handler but wrong here: this is resolved once by the SDK's singleton clients and then read on every
/// outbound call, including any that happens with no request in scope.
/// <para>
/// A singleton over <see cref="IHttpContextAccessor"/> is still correct per request - the accessor is
/// backed by an AsyncLocal, so the read inside the token minter sees the request in flight.
/// </para>
/// </remarks>
public sealed class NhsnActorEmailProvider : IActorEmailProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly NhsnJwtSettings _jwtSettings;

    public NhsnActorEmailProvider(IHttpContextAccessor httpContextAccessor, IOptions<NhsnJwtSettings> jwtOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _jwtSettings = jwtOptions.Value;
    }

    public string? CurrentEmail
    {
        get
        {
            //the email has one source in either deployment, and it is the JWT the UI presents: signed
            //in the browser from a test profile in Lantana environments, issued by the gateway at CDC.
            //An unauthenticated request therefore has no email being withheld here - it has none at
            //all. Nor is this a forgery guard: an absent or invalid bearer leaves a principal carrying
            //no claims whatsoever. It is here because User names a person, and naming one whose
            //identity was never established would have the event assert something unproven, which
            //reads worse to an investigator than an empty field. Nothing reaches this unauthenticated
            //today either - the only anonymous endpoint groups serve localization strings and static
            //files and call no Link service - and the check is what keeps that true if one that does
            //call Link is ever added.
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            //blank counts as naming nobody, so the claim is omitted rather than sent empty.
            var email = NhsnClaims.Email(user, _jwtSettings);

            return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        }
    }
}
