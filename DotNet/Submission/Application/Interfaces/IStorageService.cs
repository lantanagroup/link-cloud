using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Submission.Application.Interfaces
{
    public interface IStorageService
    {
        string DestinationType { get; }

        bool HasInternalClient();
        bool HasExternalClient();
        Task<byte[]?> DownloadFromInternalAsync(SubmitPayloadValue value, CancellationToken cancellationToken = default);
        Task UploadToExternalAsync(SubmitPayloadKey key, SubmitPayloadValue value, byte[] content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the internal submission blobs under <paramref name="payloadRootUri"/>
        /// into <paramref name="destination"/> as a ZIP, one blob at a time.
        /// </summary>
        Task WriteInternalAsZipAsync(string payloadRootUri, Stream destination, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the external submission blobs for <paramref name="payloadRootUri"/>
        /// into <paramref name="destination"/> as a ZIP, one blob at a time.
        /// </summary>
        Task WriteExternalAsZipAsync(ICollection<string> reportTypes, string payloadRootUri, Stream destination, CancellationToken cancellationToken = default);
    }
}