using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using FluentAssertions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using IntegrationTests.Normalization;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;
using ResourceCacheTypeEnum = LantanaGroup.Link.Shared.Application.Enums.ResourceCacheType;

namespace IntegrationTests.Shared;

/// <summary>
/// A batch larger than one append block, against a real Azurite.
/// </summary>
/// <remarks>
/// Appending through a write stream committed a block whenever the buffer filled, mid-line. A failure
/// after the first block left a torn line, the retry appended onto it, and every pair after the tear
/// was read back misaligned. These pin that a large batch is cut at pair boundaries and that a retry
/// after a partial batch reads back whole.
/// </remarks>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class AbsAppendAtomicityTests
{
    private const string ContainerName = "cache";
    private const int ResourceCount = 2500;

    private readonly NormalizationIntegrationTestFixture _fixture;

    public AbsAppendAtomicityTests(NormalizationIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IResourceCache Abs =>
        _fixture.ServiceProvider.GetRequiredKeyedService<IResourceCache>(ResourceCacheTypeEnum.ABS);

    private BlobContainerClient Container =>
        new BlobServiceClient(_fixture.AzuriteConnectionString).GetBlobContainerClient(ContainerName);

    [Fact]
    public async Task AppendResourcesAsync_BatchOverFourMiB_CommitsMultipleBlocksAndReadsEveryResource()
    {
        var cacheKey = $"{Guid.NewGuid()}:Observation";
        var observations = LargeObservations();

        await Container.CreateIfNotExistsAsync();
        await Abs.AppendResourcesAsync(cacheKey, [.. observations], ResourceType.Observation);

        var properties = await Container.GetAppendBlobClient(cacheKey).GetPropertiesAsync();
        properties.Value.BlobCommittedBlockCount.Should().BeGreaterThan(1, "the batch is larger than one block");

        var read = await Abs.GetAsync(cacheKey);
        read.Select(resource => resource.Id).Should().BeEquivalentTo(observations.Select(observation => observation.Id));
    }

    [Fact]
    public async Task AppendResourcesAsync_RetryAfterFirstBlockLanded_ReadsEveryResourceOnce()
    {
        var cacheKey = $"{Guid.NewGuid()}:Observation";
        var observations = LargeObservations();

        // What a failure after the first block leaves behind: that block in the payload, and no ids,
        // because the ids are appended only after the whole payload.
        var blocks = AbsPayloadFormat.BuildBlocks(
            observations.Select(observation =>
            {
                var reference = $"Observation/{observation.Id}";
                return (reference, AbsPayloadFormat.PayloadRecord(reference, observation.ToJson()));
            }),
            AbsPayloadFormat.TargetBlockBytes,
            long.MaxValue);
        blocks.Should().HaveCountGreaterThan(1);

        await Container.CreateIfNotExistsAsync();
        var payload = Container.GetAppendBlobClient(cacheKey);
        await payload.CreateIfNotExistsAsync();
        using (var firstBlock = new MemoryStream(blocks[0]))
        {
            await payload.AppendBlockAsync(firstBlock);
        }

        // The writer's retry appends the whole batch again.
        await Abs.AppendResourcesAsync(cacheKey, [.. observations], ResourceType.Observation);

        var read = await Abs.GetAsync(cacheKey);
        var count = await Abs.GetResourceCountAsync(cacheKey);

        read.Select(resource => resource.Id).Should().BeEquivalentTo(observations.Select(observation => observation.Id));
        count.Should().Be(ResourceCount);
    }

    /// <summary>
    /// About 2 KB each, so the batch spans more than one 4 MiB block.
    /// </summary>
    private static List<Observation> LargeObservations() =>
        Enumerable.Range(0, ResourceCount)
            .Select(index => new Observation
            {
                Id = $"obs-{index}",
                Status = ObservationStatus.Final,
                Code = new CodeableConcept("http://loinc.org", "8867-4"),
                Note = [new Annotation { Text = new Markdown(new string('x', 2000)) }]
            })
            .ToList();
}
