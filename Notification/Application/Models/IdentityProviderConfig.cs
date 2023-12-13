namespace LantanaGroup.Link.Notification.Application.Models
{
    public class IdentityProviderConfig
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string NameClaimType { get; set; } = string.Empty;
        public string RoleClaimType { get; set; } = string.Empty;
        public List<string>? ValidTypes { get; set; }
    }
}
