namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration
{
    public class LinkBearerServiceConfig
    {
        public bool EnableTokenGenrationEndpoint { get; set; }
        public string Authority { get; set; } = default!;
        public string? LinkAdminEmail { get; set; }
        public int TokenLifespan { get; set; } = 10;
    }
}
