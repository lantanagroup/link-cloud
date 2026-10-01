using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using FluentAssertions;
using Hl7.Fhir.Model;
using IntegrationTests.Normalization;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;
using ResourceCacheTypeEnum = LantanaGroup.Link.Shared.Application.Enums.ResourceCacheType;

namespace IntegrationTests.Shared;

/// <summary>
/// What <see cref="ABSResourceCache"/> reports as the durable resource count, against a real
/// Azurite.
/// </summary>
/// <remarks>
/// The count is not a statistic. A reader compares it against what the cache holds to decide
/// whether the cache entry is the whole record, so a count that disagrees with what
/// <see cref="ABSResourceCache.GetAsync"/> returns makes every read of that key take the slow path
/// and drop the cache entry on the way past.
/// </remarks>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class AbsResourceCountTests
{
    private const string ContainerName = "cache";

    private readonly NormalizationIntegrationTestFixture _fixture;

    public AbsResourceCountTests(NormalizationIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IResourceCache Abs =>
        _fixture.ServiceProvider.GetRequiredKeyedService<IResourceCache>(ResourceCacheTypeEnum.ABS);

    [Fact]
    public async Task GetResourceCountAsync_CountsARepeatedReferenceOnce()
    {
        var cacheKey = $"{Guid.NewGuid()}:Encounter";

        await EnsureContainerAsync();
        await Abs.AppendResourcesAsync(
            cacheKey,
            [new Encounter { Id = "enc-1" }, new Encounter { Id = "enc-2" }],
            ResourceType.Encounter);

        // Exactly what the comment on GetAsync describes: two processes appending to one key read the
        // ids blob, neither sees the other's entry, and both write the same reference. Appending
        // through the cache cannot reproduce it, because its own diff would skip the duplicate.
        await AppendRawIdsLineAsync(cacheKey, "Encounter/enc-1");

        var resources = await Abs.GetAsync(cacheKey);
        var count = await Abs.GetResourceCountAsync(cacheKey);

        // The count has to agree with the read. It is compared against the cache's own count, and a
        // durable count nothing can ever reach makes every entry for this key look partial forever.
        resources.Should().HaveCount(2);
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetDurableResourceCountAsync_SurfacesAStorageFaultAsAFault()
    {
        // Port 1 refuses immediately, so the read faults rather than hanging.
        var unreachable = new ABSResourceCache(
            Options.Create(new ResourceCacheBlobStorageSettings
            {
                ConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
                    + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
                    + "BlobEndpoint=http://127.0.0.1:1/devstoreaccount1;",
                BlobContainerName = ContainerName
            }),
            Mock.Of<ILogger<ABSResourceCache>>());

        // Reported as a cancellation while this used ContinueWith with OnlyOnRanToCompletion: a
        // faulted antecedent leaves the continuation cancelled, so the fault reached callers as an
        // OperationCanceledException -- straight past every handler written to treat a failed count
        // read as "unknown", and indistinguishable from the caller giving up.
        Exception? thrown = null;
        try
        {
            await unreachable.GetDurableResourceCountAsync($"{Guid.NewGuid()}:Encounter");
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        thrown.Should().NotBeNull("an unreachable store cannot answer the question");
        thrown.Should().NotBeAssignableTo<OperationCanceledException>(
            "nothing cancelled; reporting it that way hides a storage outage behind a handler that "
            + "exists to let real cancellation through");
    }

    private BlobContainerClient Container =>
        new BlobServiceClient(_fixture.AzuriteConnectionString).GetBlobContainerClient(ContainerName);

    /// <summary>
    /// The cache writes into the container but does not create it, and which other test created it
    /// first is not something this one should depend on.
    /// </summary>
    private async Task EnsureContainerAsync() => await Container.CreateIfNotExistsAsync();

    /// <summary>
    /// Appends one line straight to the key's ids blob, bypassing the cache's own duplicate diff.
    /// </summary>
    private async Task AppendRawIdsLineAsync(string cacheKey, string reference)
    {
        var idsBlob = Container.GetAppendBlobClient($"{cacheKey}_ids");
        await idsBlob.CreateIfNotExistsAsync();

        using var line = new MemoryStream(Encoding.UTF8.GetBytes(reference + Environment.NewLine));
        await idsBlob.AppendBlockAsync(line);
    }
}
