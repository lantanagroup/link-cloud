namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class DataProtectionSettings
    {
        public bool Enabled { get; set; }
        public string KeyRing { get; set; } = "Link";
    }
}
