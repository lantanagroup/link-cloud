using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application
{
    public class VersionInfo
    {
        public string VersionNumber { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;

        public static async Task<VersionInfo> Load()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VersionInfo.json");

            string json = await File.ReadAllTextAsync(path);

            return JsonSerializer.Deserialize<VersionInfo>(json) ?? new VersionInfo();
        }
    }
}
