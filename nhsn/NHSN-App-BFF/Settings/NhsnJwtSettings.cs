namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

public class NhsnJwtSettings
{
    public const string SectionName = "NhsnJwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string NameClaimType { get; set; } = "userLoggedInAs";
    public string EmailClaimType { get; set; } = "upn";
    public string UserIdClaimType { get; set; } = "userId";
    public string NameDisplayClaimType { get; set; } = "userName";
    public string GroupsClaimType { get; set; } = "groups";
    public string FacilityIdClaimType { get; set; } = "facility";
    public string FacilityNameClaimType { get; set; } = "facilityName";
    public string PublicCertificatePem { get; set; } = string.Empty;
    public string AccessRequestUrl { get; set; } = string.Empty;
    public int? MaxTokenAgeMinutes { get; set; }
    public string ExpiredTokenRedirectUrl { get; set; } = string.Empty;
}
