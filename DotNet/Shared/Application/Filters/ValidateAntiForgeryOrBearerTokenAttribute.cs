using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Filters;

/// <summary>
/// Validates antiforgery tokens for cookie-authenticated requests but skips validation
/// when the request carries a valid Bearer token. Bearer tokens are explicit credentials
/// that are not automatically attached by the browser, making them immune to CSRF attacks.
/// <para>
/// When anonymous access is enabled (development), antiforgery validation is always skipped
/// since there is no authentication infrastructure to protect.
/// </para>
/// <para>
/// Use this attribute instead of <see cref="ValidateAntiForgeryTokenAttribute"/> on API
/// endpoints that may be called by both browser clients (cookie auth, e.g. YARP-proxied
/// Admin.UI) and service-to-service clients (bearer auth, e.g. LinkSdk).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ValidateAntiForgeryOrBearerTokenAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
{
    /// <summary>
    /// Runs before model binding so an invalid token short-circuits early.
    /// Same order as the built-in <see cref="ValidateAntiForgeryTokenAttribute"/>.
    /// </summary>
    public int Order => 1000;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        // Bearer-authenticated requests are not vulnerable to CSRF.
        if (HasBearerToken(context))
            return;

        // When anonymous access is enabled (development), skip antiforgery validation
        // because there are no ambient credentials to protect against CSRF.
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (configuration.GetValue<bool>("Authentication:EnableAnonymousAccess"))
            return;

        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            context.Result = new AntiforgeryValidationFailedResult();
        }
    }

    private static bool HasBearerToken(AuthorizationFilterContext context)
    {
        var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();
        return !string.IsNullOrEmpty(authHeader)
               && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    }
}
