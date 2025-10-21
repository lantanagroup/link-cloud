namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class ResourceCacheBlobStorageSettings
    {
        public const string Key = "CacheBlobStorage";
        public string? ConnectionString { get; set; }
        public string? BlobContainerName { get; set; }
        public string? BlobRoot { get; set; }
    }
}
