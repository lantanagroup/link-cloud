using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class AccountServiceClient : LinkApiClientBase, IAccountServiceClient
{
    public AccountServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.AccountServiceApiUrl
                ?? throw new InvalidOperationException("Account service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    {
    }

    public Task<LinkApiResponse<PagedAccountUsersApiModel>> SearchUsersAsync(
        string? searchText,
        string? filterFacilityBy,
        string? filterRoleBy,
        string? filterClaimBy,
        bool includeDeactivatedUsers,
        bool includeDeletedUsers,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedAccountUsersApiModel>(() =>
        {
            var request = Request("/account/users")
                .SetQueryParam("includeDeactivatedUsers", includeDeactivatedUsers ? "true" : "false")
                .SetQueryParam("includeDeletedUsers", includeDeletedUsers ? "true" : "false")
                .SetQueryParam("sortBy", "UserName")
                .SetQueryParam("sortOrder", "Ascending")
                .SetQueryParam("pageSize", pageSize)
                .SetQueryParam("pageNumber", pageNumber);
            request = Set(request, "searchText", searchText);
            request = Set(request, "filterFacilityBy", filterFacilityBy);
            request = Set(request, "filterRoleBy", filterRoleBy);
            request = Set(request, "filterClaimBy", filterClaimBy);
            return request.GetAsync(cancellationToken: cancellationToken);
        });

    public Task<LinkApiResponse<AccountUserApiModel>> GetUserAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync<AccountUserApiModel>(() => Request($"/account/user/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<AccountUserApiModel>> CreateUserAsync(
        AccountUserApiModel user,
        CancellationToken cancellationToken = default) =>
        SendAsync<AccountUserApiModel>(() => Request("/account/user")
            .PostJsonAsync(user, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<AccountUserApiModel>> UpdateUserAsync(
        Guid id,
        AccountUserApiModel user,
        CancellationToken cancellationToken = default) =>
        SendAsync<AccountUserApiModel>(() => Request($"/account/user/{id}")
            .PutJsonAsync(user, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteUserAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/account/user/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RecoverUserAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/account/user/{id}/recover")
            .PostJsonAsync(new { }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<AccountClaimsApiModel>> GetClaimsAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<AccountClaimsApiModel>(() => Request("/account/claims")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateUserClaimsAsync(
        Guid id,
        IReadOnlyList<string> claims,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/account/user/{id}/claims")
            .PutJsonAsync(new AccountClaimsApiModel { Claims = claims.ToList() }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateRoleClaimsAsync(
        Guid id,
        IReadOnlyList<string> claims,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/account/role/{id}/claims")
            .PutJsonAsync(new AccountClaimsApiModel { Claims = claims.ToList() }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<AccountRoleApiModel>>> GetRolesAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<List<AccountRoleApiModel>>(() => Request("/account/role")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<AccountRoleApiModel>> GetRoleAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync<AccountRoleApiModel>(() => Request($"/account/role/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<AccountRoleApiModel>> CreateRoleAsync(
        AccountRoleApiModel role,
        CancellationToken cancellationToken = default) =>
        SendAsync<AccountRoleApiModel>(() => Request("/account/role")
            .PostJsonAsync(role, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateRoleAsync(
        Guid id,
        AccountRoleApiModel role,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/account/role/{id}")
            .PutJsonAsync(role, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteRoleAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/account/role/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    private static IFlurlRequest Set(IFlurlRequest request, string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? request : request.SetQueryParam(name, value);
}
