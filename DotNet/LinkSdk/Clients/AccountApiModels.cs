namespace LantanaGroup.Link.Sdk.Clients;

public sealed class AccountUserApiModel
{
    public Guid Id { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public List<string>? Roles { get; set; }
    public List<string>? UserClaims { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PagedAccountUsersApiModel
{
    public List<AccountUserApiModel> Records { get; set; } = [];
    public NotificationPageMetadata? Metadata { get; set; }
}

public sealed class AccountRoleApiModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Claims { get; set; }
}

public sealed class AccountClaimsApiModel
{
    public List<string> Claims { get; set; } = [];
}
