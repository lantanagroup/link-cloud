namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration
{
    public class LinkBearerServiceConfig
    {
        public bool EnableTokenGenrationEndpoint { get; set; }
        public string? LinkAdminEmail { get; set; }
        public int TokenLifespan { get; set; } = 10;
    }
}
