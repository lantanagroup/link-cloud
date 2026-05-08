namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class LinkTokenServiceSettings
    {
        public bool EnableTokenGenerationEndpoint { get; set; }
        public string Authority { get; set; } = default!;
        public string? LinkAdminEmail { get; set; }
        public int TokenLifespan { get; set; } = 10;
        public string? SigningKey { get; set; }
        public bool LogToken { get; set; } = false;
    }
}
