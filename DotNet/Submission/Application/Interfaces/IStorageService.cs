using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Submission.Application.Interfaces
{
    public interface IStorageService
    {
        string DestinationType { get; }

        bool HasInternalClient();
        Task<byte[]?> DownloadFromInternalAsync(SubmitPayloadValue value, CancellationToken cancellationToken = default);
        Task UploadToExternalAsync(SubmitPayloadKey key, SubmitPayloadValue value, byte[] content, CancellationToken cancellationToken = default);
        Task<IDictionary<string, byte[]>> DownloadFromInternalAsync(string payloadRootUri, CancellationToken cancellationToken = default);
        Task<IDictionary<string, byte[]>> DownloadFromExternalAsync(ICollection<string> reportTypes, string payloadRootUri, CancellationToken cancellationToken = default);
    }
}