namespace LantanaGroup.Link.Report.Application.Options
{
    public class BlobStorageSettings
    {
        public const string Key = "InternalBlobStorage";

        public string? ConnectionString { get; set; }
        public string? BlobContainerName { get; set; }
        public string? BlobRoot { get; set; }
    }
}
