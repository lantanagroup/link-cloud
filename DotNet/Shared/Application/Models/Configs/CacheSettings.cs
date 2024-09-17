namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class CacheSettings
    {
        public bool Enabled { get; set; }
        public string? ConnectionString { get; set; }
        public string? InstanceName { get; set; }
        public string? Password { get; set; }
        public int Timeout { get; set; }
    }
}
