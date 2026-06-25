namespace LantanaGroup.Link.Submission.Application.Config
{
    public class ExternalBlobStorageSettings : BlobStorageSettings
    {
        public const string Key = "ExternalBlobStorage";

        public bool FlattenHierarchy { get; set; }

        public bool SuppressManifest { get; set; }
    }
}
