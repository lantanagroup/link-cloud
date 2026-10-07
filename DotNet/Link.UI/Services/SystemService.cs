using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.UI.Models;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// System pages: accounts, roles, service health, the configured service addresses,
/// and the Admin.BFF integration events. Account restore and claim assignment call
/// the Account service. Consumer start, read, and stop call Admin.BFF.
/// </summary>
public sealed class SystemService
{
    public const string AccountNotConfigured = "Account service URL is not configured (ServiceRegistry:AccountServiceUrl).";
    public const string AdminNotConfigured = "Admin.BFF service URL is not configured (ServiceRegistry:AdminBffServiceUrl).";

    private readonly IAccountServiceClient? _account;
    private readonly IAdminBffIntegrationClient? _admin;
    private readonly ServiceRegistry _registry;
    private readonly bool _numericOnly;
    private readonly ILogger<SystemService> _logger;

    public SystemService(
        IAccountServiceClient? account,
        IAdminBffIntegrationClient? admin,
        ServiceRegistry registry,
        bool numericOnly,
        ILogger<SystemService> logger)
    {
        _account = account;
        _admin = admin;
        _registry = registry;
        _numericOnly = numericOnly;
        _logger = logger;
    }

    public static SystemService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        var features = services.GetRequiredService<IOptions<LinkUiFeatureOptions>>().Value;
        return new SystemService(
            string.IsNullOrWhiteSpace(registry.AccountServiceUrl) ? null : services.GetRequiredService<IAccountServiceClient>(),
            string.IsNullOrWhiteSpace(registry.AdminBffServiceUrl) ? null : services.GetRequiredService<IAdminBffIntegrationClient>(),
            registry,
            features.NumericOnlyFacilityId,
            services.GetRequiredService<ILogger<SystemService>>());
    }

    public SystemHomePage LoadHome() => new()
    {
        AccountConfigured = _account is not null,
        AdminConfigured = _admin is not null
    };

    public async Task<UserListPage> LoadUsersAsync(UserQuery query, CancellationToken cancellationToken)
    {
        var page = new UserListPage { Configured = _account is not null, Query = query };
        var error = SystemRules.CheckUserQuery(query, _numericOnly, out var clean);
        page.Query = clean;
        if (error is not null)
        {
            page.LoadError = error;
            return page;
        }

        if (_account is null)
        {
            page.LoadError = AccountNotConfigured;
            return page;
        }

        var response = await _account.SearchUsersAsync(
            clean.SearchText,
            clean.FacilityId,
            clean.Role,
            clean.Claim,
            clean.IncludeDeactivated,
            clean.IncludeDeleted,
            clean.PageSize,
            clean.Page,
            cancellationToken);
        if (response.StatusCode == 204)
            return page;
        if (!Ok(response))
        {
            page.LoadError = FailMessage("Account", response.StatusCode, response.RawBody);
            return page;
        }

        page.Users = (response.Body?.Records ?? [])
            .Select(user => new UserRow
            {
                Id = user.Id,
                Username = user.Username ?? "",
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Email = user.Email ?? "",
                Roles = user.Roles?.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role!).ToArray() ?? [],
                IsDeleted = user.IsDeleted,
                IsActive = user.IsActive
            })
            .ToArray();
        var metadata = response.Body?.Metadata;
        page.Paging = new PageBar
        {
            Page = metadata?.PageNumber > 0 ? metadata.PageNumber : clean.Page,
            PageSize = metadata?.PageSize > 0 ? metadata.PageSize : clean.PageSize,
            TotalCount = metadata?.TotalCount ?? page.Users.Count,
            TotalPages = metadata?.TotalPages ?? 0
        };
        return page;
    }

    public async Task<UserEditPage> LoadUserAsync(string? id, CancellationToken cancellationToken)
    {
        var page = new UserEditPage { Configured = _account is not null };
        if (SystemRules.CheckUserId(id, out var userId) is { } idError)
        {
            page.LoadError = idError;
            return page;
        }

        page.Id = userId;
        if (_account is null)
        {
            page.LoadError = AccountNotConfigured;
            return page;
        }

        var user = await _account.GetUserAsync(userId, cancellationToken);
        var roles = await _account.GetRolesAsync(cancellationToken);
        page.KnownRoles = RoleNames(roles);
        if (roles.StatusCode is not 204 && !Ok(roles))
            page.LoadError = FailMessage("Account", roles.StatusCode, roles.RawBody);
        if (user.StatusCode == 404)
        {
            page.LoadError = "That account was not found.";
            return page;
        }

        if (!Ok(user) || user.Body is null)
        {
            page.LoadError = FailMessage("Account", user.StatusCode, user.RawBody);
            return page;
        }

        page.Form = new UserForm
        {
            Username = user.Body.Username,
            FirstName = user.Body.FirstName,
            MiddleName = user.Body.MiddleName,
            LastName = user.Body.LastName,
            Email = user.Body.Email,
            Roles = user.Body.Roles ?? []
        };
        page.Claims = CleanClaims(user.Body.UserClaims);
        page.IsDeleted = user.Body.IsDeleted;
        page.IsActive = user.Body.IsActive;
        page.ReadOnly = user.Body.IsDeleted;
        page.Found = true;
        if (!user.Body.IsDeleted)
            await ApplyClaimCatalogAsync(page, cancellationToken);
        page.KnownRoles = page.KnownRoles
            .Concat((page.Form.Roles ?? []).Where(role => !string.IsNullOrWhiteSpace(role))!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return page;
    }

    public async Task<SystemAction> SaveUserAsync(string? id, UserForm form, CancellationToken cancellationToken)
    {
        if (_account is null)
            return SystemAction.Fail(AccountNotConfigured);

        var roles = await _account.GetRolesAsync(cancellationToken);
        if (roles.StatusCode is not 204 && !Ok(roles))
            return SystemAction.Fail(FailMessage("Account", roles.StatusCode, roles.RawBody));

        var known = RoleNames(roles);
        Guid? userId = null;
        AccountUserApiModel? existing = null;
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (SystemRules.CheckUserId(id, out var parsed) is { } idError)
                return SystemAction.Fail(idError);
            userId = parsed;
            var loaded = await _account.GetUserAsync(parsed, cancellationToken);
            if (loaded.StatusCode == 404)
                return SystemAction.Fail("That account was not found.");
            if (!Ok(loaded) || loaded.Body is null)
                return SystemAction.Fail(FailMessage("Account", loaded.StatusCode, loaded.RawBody));
            if (loaded.Body.IsDeleted)
                return SystemAction.Fail("Deleted accounts are not edited. Restore the account first.");
            existing = loaded.Body;
            known = known
                .Concat(existing.Roles ?? [])
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (SystemRules.CheckUser(form, known, out var input) is { } error)
            return SystemAction.Fail(error);

        if (existing is null)
        {
            var created = await _account.CreateUserAsync(new AccountUserApiModel
            {
                Username = input.Username,
                FirstName = input.FirstName,
                MiddleName = input.MiddleName,
                LastName = input.LastName,
                Email = input.Email,
                Roles = input.Roles.ToList(),
                UserClaims = [],
                IsActive = true
            }, cancellationToken);
            if (created.StatusCode == 409)
                return SystemAction.Fail("An account with that email already exists.");
            if (!Ok(created))
                return SystemAction.Fail(FailMessage("Account", created.StatusCode, created.RawBody));
            return SystemAction.Ok("Account created.");
        }

        var updated = await _account.UpdateUserAsync(userId!.Value, SystemRules.MergeUser(existing, input), cancellationToken);
        if (updated.StatusCode == 409)
            return SystemAction.Fail("An account with that email already exists.");
        if (!Ok(updated))
            return SystemAction.Fail(FailMessage("Account", updated.StatusCode, updated.RawBody));
        return SystemAction.Ok("Account saved.");
    }

    public async Task<SystemAction> DeleteUserAsync(string? id, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckUserId(id, out var userId) is { } error)
            return SystemAction.Fail(error);
        if (_account is null)
            return SystemAction.Fail(AccountNotConfigured);
        var response = await _account.DeleteUserAsync(userId, cancellationToken);
        if (response.StatusCode == 404)
            return SystemAction.Fail("That account was not found.");
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Account", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Account deleted.");
    }

    public async Task<SystemAction> RecoverUserAsync(string? id, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckUserId(id, out var userId) is { } error)
            return SystemAction.Fail(error);
        if (_account is null)
            return SystemAction.Fail(AccountNotConfigured);
        var response = await _account.RecoverUserAsync(userId, cancellationToken);
        if (response.StatusCode == 404)
            return SystemAction.Fail("That account was not found.");
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Account", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Account restored.");
    }

    public async Task<SystemAction> SaveUserClaimsAsync(string? id, ClaimForm form, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckUserId(id, out var userId) is { } error)
            return SystemAction.Fail(error);
        if (_account is null)
            return SystemAction.Fail(AccountNotConfigured);

        var loaded = await _account.GetUserAsync(userId, cancellationToken);
        if (loaded.StatusCode == 404)
            return SystemAction.Fail("That account was not found.");
        if (!Ok(loaded) || loaded.Body is null)
            return SystemAction.Fail(FailMessage("Account", loaded.StatusCode, loaded.RawBody));
        if (loaded.Body.IsDeleted)
            return SystemAction.Fail("Deleted accounts are not edited. Restore the account first.");

        var catalog = await _account.GetClaimsAsync(cancellationToken);
        if (!Ok(catalog) || catalog.Body is null)
            return SystemAction.Fail(catalog.StatusCode == 204
                ? "No assignable claims were returned, so claims were not changed."
                : FailMessage("Account", catalog.StatusCode, catalog.RawBody));

        var allowed = catalog.Body.Claims.Concat(loaded.Body.UserClaims ?? []).ToArray();
        if (SystemRules.CheckClaims(form.Claims, allowed, out var claims) is { } claimError)
            return SystemAction.Fail(claimError);

        var updated = await _account.UpdateUserClaimsAsync(userId, claims, cancellationToken);
        if (updated.StatusCode == 404)
            return SystemAction.Fail("That account was not found.");
        if (!Ok(updated))
            return SystemAction.Fail(FailMessage("Account", updated.StatusCode, updated.RawBody));
        return SystemAction.Ok("Claims saved.");
    }

    public async Task<RoleListPage> LoadRolesAsync(CancellationToken cancellationToken)
    {
        var page = new RoleListPage { Configured = _account is not null };
        if (_account is null)
        {
            page.LoadError = AccountNotConfigured;
            return page;
        }

        var response = await _account.GetRolesAsync(cancellationToken);
        if (response.StatusCode == 204)
            return page;
        if (!Ok(response))
        {
            page.LoadError = FailMessage("Account", response.StatusCode, response.RawBody);
            return page;
        }

        page.Roles = (response.Body ?? [])
            .Select(role => new RoleRow
            {
                Id = role.Id,
                Name = role.Name ?? "",
                Description = role.Description ?? "",
                Claims = CleanClaims(role.Claims)
            })
            .OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var catalog = await _account.GetClaimsAsync(cancellationToken);
        if (catalog.StatusCode == 204 || (Ok(catalog) && catalog.Body?.Claims is not { Count: > 0 }))
            page.ClaimsError = "No assignable claims were returned.";
        else if (!Ok(catalog) || catalog.Body is null)
            page.ClaimsError = FailMessage("Account", catalog.StatusCode, catalog.RawBody);
        else
            page.ClaimCatalog = CleanClaims(catalog.Body.Claims.Concat(page.Roles.SelectMany(role => role.Claims)));
        return page;
    }

    public async Task<SystemAction> SaveRoleAsync(RoleForm form, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckRole(form, out var name, out var description) is { } error)
            return SystemAction.Fail(error);
        if (_account is null)
            return SystemAction.Fail(AccountNotConfigured);

        if (form.Id is null || form.Id == Guid.Empty)
        {
            var created = await _account.CreateRoleAsync(new AccountRoleApiModel
            {
                Name = name,
                Description = description,
                Claims = []
            }, cancellationToken);
            if (!Ok(created))
                return SystemAction.Fail(FailMessage("Account", created.StatusCode, created.RawBody));
            return SystemAction.Ok("Role created.");
        }

        var existing = await _account.GetRoleAsync(form.Id.Value, cancellationToken);
        if (existing.StatusCode == 404)
            return SystemAction.Fail("That role was not found.");
        if (!Ok(existing) || existing.Body is null)
            return SystemAction.Fail(FailMessage("Account", existing.StatusCode, existing.RawBody));

        var updated = await _account.UpdateRoleAsync(form.Id.Value, SystemRules.MergeRole(existing.Body, name, description), cancellationToken);
        if (!Ok(updated))
            return SystemAction.Fail(FailMessage("Account", updated.StatusCode, updated.RawBody));
        return SystemAction.Ok("Role saved.");
    }

    public async Task<SystemAction> DeleteRoleAsync(string? id, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckRoleId(id, out var roleId) is { } error)
            return SystemAction.Fail(error);
        if (_account is null)
            return SystemAction.Fail(AccountNotConfigured);
        var response = await _account.DeleteRoleAsync(roleId, cancellationToken);
        if (response.StatusCode == 404)
            return SystemAction.Fail("That role was not found.");
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Account", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Role deleted.");
    }

    public async Task<SystemAction> SaveRoleClaimsAsync(string? id, ClaimForm form, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckRoleId(id, out var roleId) is { } error)
            return SystemAction.Fail(error);
        if (_account is null)
            return SystemAction.Fail(AccountNotConfigured);

        var loaded = await _account.GetRoleAsync(roleId, cancellationToken);
        if (loaded.StatusCode == 404)
            return SystemAction.Fail("That role was not found.");
        if (!Ok(loaded) || loaded.Body is null)
            return SystemAction.Fail(FailMessage("Account", loaded.StatusCode, loaded.RawBody));

        var catalog = await _account.GetClaimsAsync(cancellationToken);
        if (!Ok(catalog) || catalog.Body is null)
            return SystemAction.Fail(catalog.StatusCode == 204
                ? "No assignable claims were returned, so claims were not changed."
                : FailMessage("Account", catalog.StatusCode, catalog.RawBody));

        var allowed = catalog.Body.Claims.Concat(loaded.Body.Claims ?? []).ToArray();
        if (SystemRules.CheckClaims(form.Claims, allowed, out var claims) is { } claimError)
            return SystemAction.Fail(claimError);

        var updated = await _account.UpdateRoleClaimsAsync(roleId, claims, cancellationToken);
        if (updated.StatusCode == 404)
            return SystemAction.Fail("That role was not found.");
        if (!Ok(updated))
            return SystemAction.Fail(FailMessage("Account", updated.StatusCode, updated.RawBody));
        return SystemAction.Ok("Claims saved.");
    }

    public async Task<HealthPage> LoadHealthAsync(string? service, CancellationToken cancellationToken)
    {
        var page = new HealthPage { Configured = _admin is not null, Service = service?.Trim() };
        if (SystemRules.CheckHealthService(service, out var key) is { } error)
        {
            page.HealthError = error;
            return page;
        }

        if (_admin is null)
        {
            page.HealthError = AdminNotConfigured;
            page.InfoError = AdminNotConfigured;
            return page;
        }

        if (key is not null)
        {
            var one = await _admin.GetServiceHealthAsync(key, cancellationToken);
            if (!Ok(one))
                page.HealthError = FailMessage("Admin.BFF", one.StatusCode, one.RawBody);
            else if (SystemRules.ReadHealth(one.Body, out var oneRow) is { } parseError)
                page.HealthError = parseError;
            else
                page.Reports = oneRow;
            return page;
        }

        var health = await _admin.GetHealthAsync(cancellationToken);
        if (health.StatusCode == 204)
            page.Reports = Array.Empty<HealthRow>();
        else if (!Ok(health))
            page.HealthError = FailMessage("Admin.BFF", health.StatusCode, health.RawBody);
        else if (SystemRules.ReadHealth(health.Body, out var rows) is { } healthParse)
            page.HealthError = healthParse;
        else
            page.Reports = rows;

        var info = await _admin.GetServiceInformationAsync(cancellationToken);
        if (info.StatusCode == 204)
            page.Services = Array.Empty<ServiceInfoRow>();
        else if (!Ok(info))
            page.InfoError = FailMessage("Admin.BFF", info.StatusCode, info.RawBody);
        else if (SystemRules.ReadServiceInfo(info.Body, out var services) is { } infoParse)
            page.InfoError = infoParse;
        else
            page.Services = services;
        return page;
    }

    public AppConfigurationPage LoadAppConfiguration()
    {
        var (rows, missing) = SystemRules.ReadAddresses(_registry);
        return new AppConfigurationPage { Addresses = rows, Missing = missing };
    }

    public IntegrationPage LoadIntegration() => new()
    {
        Configured = _admin is not null,
        NumericOnlyFacilityId = _numericOnly
    };

    public async Task<SystemAction> ScheduleReportAsync(ReportScheduledForm form, CancellationToken cancellationToken)
    {
        var error = SystemRules.CheckReportScheduled(form, _numericOnly, DateTime.UtcNow, out var request);
        if (error is not null || request is null)
            return SystemAction.Fail(error ?? "The report could not be checked.");
        if (_admin is null)
            return SystemAction.Fail(AdminNotConfigured);
        if (!Enum.TryParse<Frequency>(request.Frequency, out var frequency))
            return SystemAction.Fail("Frequency must be Discharge, Daily, Weekly, Monthly, or Adhoc.");

        var tracking = request.ReportTrackingId ?? Guid.NewGuid();
        var response = await _admin.CreateReportScheduledAsync(
            request.FacilityId,
            frequency,
            request.ReportTypes,
            request.StartDateUtc,
            request.DelayMinutes,
            tracking.ToString(),
            cancellationToken);
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Admin.BFF", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Report scheduled event posted. Tracking id " + tracking + ".");
    }

    public async Task<SystemAction> AcquirePatientListAsync(PatientListForm form, CancellationToken cancellationToken)
    {
        var error = SystemRules.CheckPatientList(form, _numericOnly, out var request);
        if (error is not null || request is null)
            return SystemAction.Fail(error ?? "The patient list could not be checked.");
        if (_admin is null)
            return SystemAction.Fail(AdminNotConfigured);
        if (!Enum.TryParse<ListType>(request.ListType, out var listType) || !Enum.TryParse<TimeFrame>(request.TimeFrame, out var timeFrame))
            return SystemAction.Fail("List type or time frame was not recognized.");

        var tracking = request.ReportTrackingId ?? Guid.NewGuid();
        var response = await _admin.CreatePatientListAcquiredAsync(
            request.FacilityId,
            [
                new PatientListItem
                {
                    ListType = listType,
                    TimeFrame = timeFrame,
                    PatientIds = request.PatientIds.ToList()
                }
            ],
            tracking,
            cancellationToken);
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Admin.BFF", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Patient list acquired event posted. Tracking id " + tracking + ".");
    }

    public async Task<SystemAction> PostPatientEventAsync(PatientEventForm form, CancellationToken cancellationToken)
    {
        var error = SystemRules.CheckPatientEvent(form, _numericOnly, out var request);
        if (error is not null || request is null)
            return SystemAction.Fail(error ?? "The patient event could not be checked.");
        if (_admin is null)
            return SystemAction.Fail(AdminNotConfigured);
        var response = await _admin.CreatePatientEventAsync(request.FacilityId, request.PatientId, request.EventType, cancellationToken);
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Admin.BFF", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Patient event posted.");
    }

    public async Task<SystemAction> PostDataAcquisitionAsync(DataAcquisitionForm form, CancellationToken cancellationToken)
    {
        var error = SystemRules.CheckDataAcquisition(form, _numericOnly, DateTime.UtcNow, out var request);
        if (error is not null || request is null)
            return SystemAction.Fail(error ?? "The data acquisition event could not be checked.");
        if (_admin is null)
            return SystemAction.Fail(AdminNotConfigured);
        var response = await _admin.CreateDataAcquisitionRequestedAsync(
            request.FacilityId,
            request.PatientId,
            request.QueryType,
            request.ReportTypes,
            request.StartDateUtc,
            request.EndDateUtc,
            cancellationToken);
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Admin.BFF", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Data acquisition requested event posted.");
    }

    public async Task<SystemAction> PostPatientAcquiredAsync(PatientAcquiredForm form, CancellationToken cancellationToken)
    {
        var error = SystemRules.CheckPatientAcquired(form, _numericOnly, out var request);
        if (error is not null || request is null)
            return SystemAction.Fail(error ?? "The patient acquired event could not be checked.");
        if (_admin is null)
            return SystemAction.Fail(AdminNotConfigured);
        var tracking = request.ReportTrackingId ?? Guid.NewGuid();
        var response = await _admin.CreatePatientAcquiredAsync(
            request.FacilityId,
            request.PatientIds,
            tracking.ToString(),
            cancellationToken);
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Admin.BFF", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Patient acquired event posted. Tracking id " + tracking + ".");
    }

    public async Task<SystemAction> StartConsumersAsync(string? correlationId, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckCorrelation(correlationId, generate: true, out var correlation) is { } error)
            return SystemAction.Fail(error);
        if (_admin is null)
            return SystemAction.Fail(AdminNotConfigured);
        var response = await _admin.StartConsumersAsync(correlation.ToString(), cancellationToken);
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Admin.BFF", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Consumers started. Correlation id " + correlation + ".");
    }

    public async Task<IntegrationPage> ReadConsumersAsync(string? correlationId, CancellationToken cancellationToken)
    {
        var page = LoadIntegration();
        if (SystemRules.CheckCorrelation(correlationId, generate: false, out var correlation) is { } error)
        {
            page.ReadError = error;
            return page;
        }

        if (_admin is null)
        {
            page.ReadError = AdminNotConfigured;
            return page;
        }

        var response = await _admin.ReadConsumersAsync(correlation.ToString(), cancellationToken);
        if (!Ok(response))
        {
            page.ReadError = FailMessage("Admin.BFF", response.StatusCode, response.RawBody);
            return page;
        }

        if (SystemRules.ReadConsumers(response.Body, out var topics) is { } parseError)
        {
            page.ReadError = parseError;
            return page;
        }

        page.Topics = topics;
        page.ReadNote = topics.Count == 0
            ? "No consumer events for " + correlation + "."
            : "Consumer events for " + correlation + ".";
        return page;
    }

    public async Task<SystemAction> StopConsumersAsync(string? correlationId, CancellationToken cancellationToken)
    {
        if (SystemRules.CheckCorrelation(correlationId, generate: false, out var correlation) is { } error)
            return SystemAction.Fail(error);
        if (_admin is null)
            return SystemAction.Fail(AdminNotConfigured);
        var response = await _admin.StopConsumersAsync(correlation.ToString(), cancellationToken);
        if (!Ok(response))
            return SystemAction.Fail(FailMessage("Admin.BFF", response.StatusCode, response.RawBody));
        return SystemAction.Ok("Consumers stopped. Correlation id " + correlation + ".");
    }

    private async Task ApplyClaimCatalogAsync(UserEditPage page, CancellationToken cancellationToken)
    {
        var catalog = await _account!.GetClaimsAsync(cancellationToken);
        if (catalog.StatusCode == 204 || (Ok(catalog) && catalog.Body?.Claims is not { Count: > 0 }))
            page.ClaimsError = "No assignable claims were returned.";
        else if (!Ok(catalog) || catalog.Body is null)
            page.ClaimsError = FailMessage("Account", catalog.StatusCode, catalog.RawBody);
        else
            page.ClaimCatalog = CleanClaims(catalog.Body.Claims.Concat(page.Claims));
    }

    private static IReadOnlyList<string> CleanClaims(IEnumerable<string>? claims) =>
        (claims ?? [])
        .Where(claim => !string.IsNullOrWhiteSpace(claim))
        .Select(claim => claim.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(claim => claim, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<string> RoleNames(LinkApiResponse<List<AccountRoleApiModel>> response) =>
        (response.Body ?? [])
        .Select(role => role.Name)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void Note(string action, int statusCode) =>
        _logger.LogDebug("System {Action} returned {StatusCode}", action, statusCode);

    private string FailMessage(string service, int statusCode, string? body)
    {
        Note(service, statusCode);
        return FacilityFormRules.ServiceMessage(service, statusCode, body);
    }

    private static bool Ok(LinkApiResponse response) => response.StatusCode is >= 200 and < 300;

    private static bool Ok<T>(LinkApiResponse<T> response) => response.StatusCode is >= 200 and < 300;
}
