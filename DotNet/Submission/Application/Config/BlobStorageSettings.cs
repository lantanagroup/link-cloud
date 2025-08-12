namespace LantanaGroup.Link.Submission.Application.Config
{
    public class BlobStorageSettings
    {
        public string? ConnectionString { get; set; }
        public string? BlobContainerName { get; set; }
        public string? BlobRoot { get; set; }
    }
}
