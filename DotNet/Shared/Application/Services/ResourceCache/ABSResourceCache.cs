using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    public class ABSResourceCache : IResourceCache
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ResourceCacheBlobStorageSettings _settings;
        private readonly ILogger<ABSResourceCache> _logger;

        public ABSResourceCache(IOptions<ResourceCacheBlobStorageSettings> settings, ILogger<ABSResourceCache> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
        }

        private string GetBlobKey(string key) =>
            string.IsNullOrEmpty(_settings.BlobRoot) ? key : $"{_settings.BlobRoot}/{key}";

        private string GetBlobIdsKey(string key) =>
            GetBlobKey(key) + "_ids";

        public async Task AppendResourcesAsync(string cacheKey, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            string blobName = GetBlobKey(cacheKey);
            string idsBlobName = GetBlobIdsKey(cacheKey);
            
            //First read the existing blob to get the list of resource references that are already in the cache. 
            // This is necessary because we want to append new resources to the existing blob, 
            // and we don't want to write duplicate resource references if there are multiple 
            // resources of the same type in the same batch.
            HashSet<string> existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var readBlobClient = _containerClient.GetBlobClient(idsBlobName);
            if ((await readBlobClient.ExistsAsync(cancellationToken)).Value)
            {
                await using (Stream readStream = await readBlobClient.OpenReadAsync(cancellationToken: cancellationToken))
                using (StreamReader reader = new StreamReader(readStream))
                {
                    while (true)
                    {
                        string? id = await reader.ReadLineAsync(cancellationToken);
                        if (id == null)
                        {
                            break;
                        }

                        if(!string.IsNullOrEmpty(id))
                        {
                            existingIds.Add(id);
                        }
                    }
                }
            }

            var resourcesToWrite = new Dictionary<string, DomainResource>();
            foreach(var resource in resources)
            {
                var referenceId = resource.TypeName + "/" + resource.Id;
                if (!existingIds.Contains(referenceId))
                {
                    resourcesToWrite[referenceId] = resource;
                }
            }

            if(!resourcesToWrite.Any())
                return;

            AppendBlobClient writeBlobClient = _containerClient.GetAppendBlobClient(blobName);
            AppendBlobClient writeIdsBlobClient = _containerClient.GetAppendBlobClient(idsBlobName);
            await writeBlobClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await writeIdsBlobClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            
            await AppendBlocksAsync(
                writeBlobClient,
                resourcesToWrite.Select(resource =>
                    (resource.Key, AbsPayloadFormat.PayloadRecord(resource.Key, resource.Value.ToJson()))),
                cancellationToken);

            await AppendBlocksAsync(
                writeIdsBlobClient,
                resourcesToWrite.Keys.Select(reference => (reference, AbsPayloadFormat.IdRecord(reference))),
                cancellationToken);
        }

        /// <remarks>
        /// One AppendBlock call per block, never a write stream. The stream commits a block whenever its
        /// buffer fills, mid-line, so a failure after the first block left a torn line that the retry then
        /// appended onto, and every pair after it was read back misaligned.
        /// </remarks>
        private static async Task AppendBlocksAsync(AppendBlobClient blobClient,
                                                    IEnumerable<(string Reference, string Record)> records,
                                                    CancellationToken cancellationToken)
        {
            var blocks = AbsPayloadFormat.BuildBlocks(
                records,
                AbsPayloadFormat.TargetBlockBytes,
                blobClient.AppendBlobMaxAppendBlockBytes);

            foreach (var block in blocks)
            {
                using var content = new MemoryStream(block, writable: false);
                await blobClient.AppendBlockAsync(content, cancellationToken: cancellationToken);
            }
        }

        public async Task<List<DomainResource>> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            if (_containerClient == null)
            {
                throw new Exception("ABS Container Client is null when attempting to get resource cache");
            }

            List<DomainResource> resources = new List<DomainResource>();

            // Writes use AppendBlobClient. Reads must use the generic BlobClient: BlockBlobClient
            // ExistsAsync/OpenReadAsync against an append blob reports not-found, which
            // Normalization then treated as an empty cache and deleted the real source blobs.
            BlobClient readBlobClient = _containerClient.GetBlobClient(GetBlobKey(cacheKey));

            if (!(await readBlobClient.ExistsAsync(cancellationToken)).Value)
            {
                _logger.LogWarning("ABS blob not found for Get. CacheKey='{CacheKey}', BlobPath='{BlobPath}', Container='{Container}'",
                    cacheKey.SanitizeForLog(),
                    GetBlobKey(cacheKey).SanitizeForLog(),
                    _settings.BlobContainerName.SanitizeForLog());
                return resources;
            }

            // The payload blob can legitimately contain the same reference twice: a crash between the
            // payload append and the ids append leaves a resource the retry's diff will not skip, a
            // retried append repeats whole blocks, and two processes appending to one key can
            // interleave. ReadPairsAsync collapses them -- wasted bytes rather than duplicate resources.
            int duplicateCount;

            await using (Stream readStream = await readBlobClient.OpenReadAsync(true, cancellationToken: cancellationToken))
            using (StreamReader reader = new StreamReader(readStream))
            {
                duplicateCount = await AbsPayloadFormat.ReadPairsAsync(
                    reader,
                    (resourceReference, resourceString) =>
                    {
                        try
                        {
                            DomainResource resource = JsonSerializer.Deserialize<DomainResource>(resourceString, LinkFhirSerializerOptions.ForFhirLenientSerialization);
                            resources.Add(resource);
                        }
                        catch (Exception)
                        {
                            //We aren't going to dead letter the event if we have issues deserializing the resource, but will log it.
                            _logger.LogError("Failed to deserialize FHIR DomainResource for the following ABS entry: {reference}", resourceReference.SanitizeForLog());
                        }
                    },
                    cancellationToken);
            }

            if (duplicateCount > 0)
            {
                _logger.LogWarning(
                    "Collapsed {DuplicateCount} duplicate reference(s) reading {CacheKey}. Expected after an interrupted or concurrent write; persistent growth here means writes to one key are not being serialized.",
                    duplicateCount,
                    cacheKey.SanitizeForLog());
            }

            return resources;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Sequential rather than atomic: this store has no transaction, and it does not need one.
        /// Nothing shadows it, so a reader arriving between the two steps reads an empty key and gets an
        /// empty answer rather than a stale one -- and the callers that replace do so before anything
        /// downstream is told the key exists.
        /// </remarks>
        public async Task ReplaceResourcesAsync(
            string cacheKey,
            List<DomainResource> resources,
            ResourceType resourceType,
            CancellationToken cancellationToken = default)
        {
            await DeleteAsync([cacheKey], cancellationToken);

            if (resources != null && resources.Count > 0)
            {
                await AppendResourcesAsync(cacheKey, resources, resourceType, cancellationToken);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Read from the ids blob rather than the payload, so the cost is one small listing of resource
        /// references instead of deserializing every resource.
        /// </remarks>
        public async Task<int> GetResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var idsBlobClient = _containerClient.GetBlobClient(GetBlobIdsKey(cacheKey));

            if (!(await idsBlobClient.ExistsAsync(cancellationToken)).Value)
            {
                return 0;
            }

            // Counted as distinct references, exactly as GetAsync returns them. The ids blob can hold
            // the same reference twice -- a crash between the two appends, or two processes
            // interleaving on one key -- and counting those again would make the durable count
            // exceed what any reader can ever see, so every cache entry for the key would be judged
            // partial and every read would fall back here.
            var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var stream = await idsBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
            using var reader = new StreamReader(stream);

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    references.Add(line);
                }
            }

            return references.Count;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This store is the durable one, so its own count is the answer and there is nothing recorded.
        /// </remarks>
        public async Task<int?> GetDurableResourceCountAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            // Awaited rather than continued. OnlyOnRanToCompletion turns a faulted read into a
            // *cancelled* task, so a storage fault reached callers as an OperationCanceledException
            // -- past every handler written to treat a failed count read as "unknown", and
            // indistinguishable from the caller cancelling.
            return await GetResourceCountAsync(cacheKey, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ignored. Nothing shadows this store, so it has no partial-entry problem to detect.
        /// </remarks>
        public Task SetDurableResourceCountAsync(string cacheKey, int count, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        /// <inheritdoc/>
        /// <remarks>
        /// Always true, for the same reason: this store is the record.
        /// </remarks>
        public Task<bool> IsEntryCompleteAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
        {
            string[] splitKey = cacheKey.Split(":");

            if (splitKey.Length != 2)
            {
                throw new Exception($"Cache key '{cacheKey}' does not contain required ':' divider. Expected format is <correlation id>:<resource type>");
            }

            if (Enum.TryParse<ResourceType>(splitKey[1], out var resourceType))
            {
                return resourceType;
            }
            else
            {
                throw new Exception($"Could not parse the ABS cache key '{cacheKey}' into a valid FHIR Resource Type");
            }
        }

        /// <summary>
        /// Completes immediately: this cache writes durably in-line, so there is never a queued write
        /// outstanding.
        /// </summary>
        public Task WaitForDurableAsync(string correlationId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Completes immediately: this store is the durable one, so a write that returned has landed.
        /// </summary>
        public Task WaitForDurableAsync(IEnumerable<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            // The ids append blob is only created when at least one resource was written.
            return (await _containerClient.GetBlobClient(GetBlobIdsKey(cacheKey)).ExistsAsync(cancellationToken)).Value;
        }

        public async Task DeleteAsync(List<string> cacheKeys, CancellationToken cancellationToken = default)
        {
            foreach (var cacheKey in cacheKeys)
            {
                await _containerClient.DeleteBlobIfExistsAsync(GetBlobKey(cacheKey), cancellationToken: cancellationToken);
                await _containerClient.DeleteBlobIfExistsAsync(GetBlobIdsKey(cacheKey), cancellationToken: cancellationToken);
            }
        }
    }
}
