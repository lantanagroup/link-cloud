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

        public async Task UpdateCorrelationCacheAsync(string correlationId, List<DomainResource> resources, ResourceType resourceType, CancellationToken cancellationToken = default)
        {
            string blobName = GetBlobKey(correlationId);
            string idsBlobName = GetBlobIdsKey(correlationId);
            
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
            
            await using (Stream writeStream = await writeBlobClient.OpenWriteAsync(false, cancellationToken: cancellationToken))
            await using (StreamWriter writer = new StreamWriter(writeStream))
            {
                foreach (var resourceToWrite in resourcesToWrite)
                {
                    await writer.WriteLineAsync(resourceToWrite.Key.AsMemory(), cancellationToken);
                    await writer.WriteLineAsync(resourceToWrite.Value.ToJson().AsMemory(), cancellationToken);
                }

                await writer.FlushAsync(cancellationToken);
            }

            await using (Stream idsWriteStream = await writeIdsBlobClient.OpenWriteAsync(false, cancellationToken: cancellationToken))
            await using (StreamWriter idsWriter = new StreamWriter(idsWriteStream))
            {
                foreach(var id in resourcesToWrite.Keys)
                {
                    await idsWriter.WriteLineAsync(id.AsMemory(), cancellationToken);
                }

                await idsWriter.FlushAsync(cancellationToken);
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

            await using (Stream readStream = await readBlobClient.OpenReadAsync(true, cancellationToken: cancellationToken))
            using (StreamReader reader = new StreamReader(readStream))
            {
                while (true)
                {
                    //Skip first line. It's the reference of the resource, not the resource itself
                    string? resourceReference = await reader.ReadLineAsync(cancellationToken);
                    if (resourceReference == null)
                    {
                        break;
                    }

                    string? resourceString = await reader.ReadLineAsync(cancellationToken);
                    if (resourceString == null)
                    {
                        break;
                    }

                    try
                    {
                        DomainResource resource = JsonSerializer.Deserialize<DomainResource>(resourceString, LinkFhirSerializerOptions.ForFhirLenientSerialization);
                        resources.Add(resource);
                    }
                    catch (Exception ex)
                    {
                        //We aren't going to dead letter the event if we have issues deserializing the resource, but will log it. 
                        _logger.LogError("Failed to deserialize FHIR DomainResource for the following ABS entry: {reference}", resourceReference);
                    }
                }
            }

            return resources;
        }

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

        public ResourceCacheType GetCacheTypeForCorrelationId(string correlationId)
        {
            return ResourceCacheType.ABS;
        }

        public Task<ResourceCacheType> GetCacheTypeForCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResourceCacheType.ABS);
        }

        public IResourceCache GetImplementation(ResourceCacheType cacheType)
        {
            if (cacheType != ResourceCacheType.ABS)
                throw new NotSupportedException($"{nameof(ABSResourceCache)} does not support cache type '{cacheType}'.");
            return this;
        }

        public async Task<bool> HasResourcesAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            // The ids append blob is only created when at least one resource was written.
            return (await _containerClient.GetBlobClient(GetBlobIdsKey(cacheKey)).ExistsAsync(cancellationToken)).Value;
        }

        public void ForgetCacheTypeForCorrelationId(string correlationId)
        {
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
