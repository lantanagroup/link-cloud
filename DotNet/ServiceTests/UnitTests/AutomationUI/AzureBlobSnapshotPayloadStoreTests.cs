using Automation.UI.Services.Persistence;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AzureBlobSnapshotPayloadStoreTests
{
    [Fact]
    public async Task DeleteRunPayloadsAsync_missing_container_404_is_noop()
    {
        var store = CreateStore(new ThrowingAsyncPageable<BlobItem>(new RequestFailedException(404, "Container not found")));

        var act = () => store.DeleteRunPayloadsAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteRunPayloadsAsync_non404_from_blob_list_is_propagated()
    {
        var store = CreateStore(new ThrowingAsyncPageable<BlobItem>(new RequestFailedException(500, "Server error")));

        var act = () => store.DeleteRunPayloadsAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<RequestFailedException>();
    }

    private static AzureBlobSnapshotPayloadStore CreateStore(AsyncPageable<BlobItem> pageable)
    {
        return new AzureBlobSnapshotPayloadStore(
            new ImportedBundleBlobStorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "automation-snapshot-tests"
            },
            new BlobContainerClient("UseDevelopmentStorage=true", "automation-snapshot-tests"),
            listBlobs: (_, _) => pageable,
            deleteBlob: (_, _) => Task.CompletedTask);
    }

    private sealed class ThrowingAsyncPageable<T>(RequestFailedException exception) : AsyncPageable<T> where T : notnull
    {
        public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw exception;

        public override IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null) => throw exception;
    }
}
