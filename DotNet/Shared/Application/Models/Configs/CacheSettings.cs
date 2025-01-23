using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class CacheSettings
    {
        public CacheType Type { get; set; }
        public string? ConnectionString { get; set; }
        public string? InstanceName { get; set; }
        public string? Password { get; set; }
        public int Timeout { get; set; }
    }

    public enum CacheType
    {
         InMemory,
         Redis
    }
}
