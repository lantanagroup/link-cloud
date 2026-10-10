using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.UI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services;

/// <summary>The user who started a run. Background service calls name this user to Admin.BFF.</summary>
public sealed record AutomationRunInitiator(string Id, string? Name)
{
    /// <summary>Used when no signed-in user started the work (hosted cleanup, anonymous dev shells).</summary>
    public static readonly AutomationRunInitiator System = new("system:link-ui", "Link.UI");

    public static readonly AutomationRunInitiator LeftoverCleanup = new("system:link-ui-leftover-cleanup", "Link.UI leftover run cleanup");

    public static AutomationRunInitiator FromUser(AdminBffUser? user)
    {
        if (user is not { IsAuthenticated: true })
            return System;
        var id = !string.IsNullOrWhiteSpace(user.Email) ? user.Email : user.UserName;
        return string.IsNullOrWhiteSpace(id) ? System : new AutomationRunInitiator(id.Trim(), user.UserName);
    }
}

/// <summary>
/// Marks the current async flow as background work started by <see cref="AutomationRunInitiator"/>.
/// Inside a scope, Admin.BFF calls use the system token with the initiator headers, even while the
/// originating request is still open, so a run never switches identity half way through.
/// </summary>
public static class BackgroundServiceCallScope
{
    private static readonly AsyncLocal<AutomationRunInitiator?> CurrentInitiator = new();

    public static AutomationRunInitiator? Current => CurrentInitiator.Value;

    public static IDisposable Begin(AutomationRunInitiator initiator)
    {
        ArgumentNullException.ThrowIfNull(initiator);
        var previous = CurrentInitiator.Value;
        CurrentInitiator.Value = initiator;
        return new Restore(previous);
    }

    private sealed class Restore(AutomationRunInitiator? previous) : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            CurrentInitiator.Value = previous;
        }
    }
}

/// <summary>
/// Picks the credential for each LinkSDK call sent through Admin.BFF:
/// background scope → system token plus initiator; request in progress → the user's own cookie and
/// Authorization only (no system token, even when the request has none); no request and no scope →
/// system token naming <see cref="AutomationRunInitiator.System"/>.
/// </summary>
public sealed class AdminBffCallCredentialSource(IHttpContextAccessor httpContextAccessor) : ILinkCallCredentialSource
{
    public LinkCallCredential Resolve()
    {
        if (BackgroundServiceCallScope.Current is { } initiator)
            return LinkCallCredential.SystemOnBehalfOf(initiator.Id, initiator.Name);

        var context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            return LinkCallCredential.ForwardUser(
                context.Request.Headers.Cookie.ToString(),
                context.Request.Headers.Authorization.ToString());
        }

        return LinkCallCredential.SystemOnBehalfOf(AutomationRunInitiator.System.Id, AutomationRunInitiator.System.Name);
    }
}

public static class AdminBffServiceCallRegistration
{
    /// <summary>Set to false to send LinkSDK calls straight to each service again.</summary>
    public const string ConfigKey = "Automation:RouteServiceCallsThroughAdminBff";

    /// <summary>
    /// Replaces the LinkSDK service clients Automation and the Link.UI pages use with instances whose
    /// base URL is Admin.BFF. Paths, models and response handling are unchanged. Account, audit,
    /// notification and <see cref="IAdminBffIntegrationClient"/> keep their registrations.
    /// </summary>
    public static IServiceCollection AddServiceCallsThroughAdminBff(this IServiceCollection services)
    {
        services.TryAddSingleton<AdminBffCallCredentialSource>();

        Replace<ICensusServiceClient>(services, (r, sp) => new CensusServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<IReportServiceClient>(services, (r, sp) => new ReportServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<IDataAcquisitionServiceClient>(services, (r, sp) => new DataAcquisitionServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<IFacilityServiceClient>(services, (r, sp) => new FacilityServiceClient(r, sp.GetRequiredService<IOptions<ServiceRegistry>>(), Bearer(sp), Token(sp), Mint(sp)));
        Replace<INormalizationServiceClient>(services, (r, sp) => new NormalizationServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<IQueryDispatchServiceClient>(services, (r, sp) => new QueryDispatchServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<IMeasureEvalServiceClient>(services, (r, sp) => new MeasureEvalServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<IValidationServiceClient>(services, (r, sp) => new ValidationServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<IDmrpServiceClient>(services, (r, sp) => new DmrpServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<ISubmissionServiceClient>(services, (r, sp) => new SubmissionServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        Replace<ITerminologyServiceClient>(services, (r, sp) => new TerminologyServiceClient(r, Bearer(sp), Token(sp), Mint(sp)));
        return services;
    }

    private static void Replace<T>(IServiceCollection services, Func<AdminBffRoute, IServiceProvider, T> create) where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton<T>(sp => create(Route(sp), sp));
    }

    private static AdminBffRoute Route(IServiceProvider sp) =>
        AdminBffRoute.FromRegistry(
            sp.GetRequiredService<IOptions<ServiceRegistry>>().Value,
            sp.GetRequiredService<AdminBffCallCredentialSource>());

    private static IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> Bearer(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>>();

    private static IOptions<LinkTokenServiceSettings> Token(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<LinkTokenServiceSettings>>();

    private static ICreateSystemToken Mint(IServiceProvider sp) => sp.GetRequiredService<ICreateSystemToken>();
}
