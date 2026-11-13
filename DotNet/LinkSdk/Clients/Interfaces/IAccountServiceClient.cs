using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IAccountServiceClient
{
    Task<LinkApiResponse<PagedAccountUsersApiModel>> SearchUsersAsync(
        string? searchText,
        string? filterFacilityBy,
        string? filterRoleBy,
        string? filterClaimBy,
        bool includeDeactivatedUsers,
        bool includeDeletedUsers,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<AccountUserApiModel>> GetUserAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<AccountUserApiModel>> CreateUserAsync(
        AccountUserApiModel user,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<AccountUserApiModel>> UpdateUserAsync(
        Guid id,
        AccountUserApiModel user,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteUserAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> RecoverUserAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<AccountClaimsApiModel>> GetClaimsAsync(
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> UpdateUserClaimsAsync(
        Guid id,
        IReadOnlyList<string> claims,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> UpdateRoleClaimsAsync(
        Guid id,
        IReadOnlyList<string> claims,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<List<AccountRoleApiModel>>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<AccountRoleApiModel>> GetRoleAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<AccountRoleApiModel>> CreateRoleAsync(
        AccountRoleApiModel role,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> UpdateRoleAsync(
        Guid id,
        AccountRoleApiModel role,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteRoleAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
