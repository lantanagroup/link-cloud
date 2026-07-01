using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCompress.Common;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    public class ABSResourceCache : IResourceCache
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ResourceCacheBlobStorageSettings _settings;
        private readonly ILogger<RedisResourceCache> _logger;

        public ABSResourceCache(IOptions<ResourceCacheBlobStorageSettings> settings, ILogger<RedisResourceCache> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
        }

        private string GetBlobKey(string key) =>
            string.IsNullOrEmpty(_settings.BlobRoot) ? key : $"{_settings.BlobRoot}/{key}";

        private string GetBlobIdsKey(string key) =>
            GetBlobKey(key) + "_ids";

        public void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType)
        {
            string blobName = GetBlobKey(correlationId);
            string idsBlobName = GetBlobIdsKey(correlationId);
            
            //First read the existing blob to get the list of resource references that are already in the cache. 
            // This is necessary because we want to append new resources to the existing blob, 
            // and we don't want to write duplicate resource references if there are multiple 
            // resources of the same type in the same batch.
            HashSet<string> existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var readBlobClient = _containerClient.GetBlobClient(idsBlobName);
            if(readBlobClient.Exists())
            {
                using (Stream read_stream = readBlobClient.OpenRead())
                using (StreamReader reader = new StreamReader(read_stream))
                {
                    while (reader.Peek() >= 0)
                    {
                        string? id = reader.ReadLine();
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
            writeBlobClient.CreateIfNotExists();
            writeIdsBlobClient.CreateIfNotExists();
            
            using (Stream write_stream = writeBlobClient.OpenWrite(false))
            using (StreamWriter writer = new StreamWriter(write_stream)) 
            {
                foreach (var resourceToWrite in resourcesToWrite)
                {
                    writer.WriteLine(resourceToWrite.Key);
                    writer.WriteLine(resourceToWrite.Value.ToJson());
                }
            }

            using (Stream ids_write_stream = writeIdsBlobClient.OpenWrite(false))
            using (StreamWriter ids_writer = new StreamWriter(ids_write_stream))
            {
                foreach(var id in resourcesToWrite.Keys)
                {
                    ids_writer.WriteLine(id);
                }
            }
        }

        public List<DomainResource> Get(string cacheKey)
        {
            if (_containerClient == null)
            {
                throw new Exception("ABS Container Client is null when attempting to get resource cache");
            }

            List<DomainResource> resources = new List<DomainResource>();

            BlockBlobClient readBlobClient = _containerClient.GetBlockBlobClient(GetBlobKey(cacheKey));

            if (!readBlobClient.Exists())
            {
                _logger.LogWarning("ABS blob not found for Get. CacheKey='{CacheKey}', BlobPath='{BlobPath}', Container='{Container}'",
                    cacheKey, GetBlobKey(cacheKey), _settings.BlobContainerName);
                return resources;
            }

            using (Stream read_stream = readBlobClient.OpenRead(true))
            using (StreamReader reader = new StreamReader(read_stream))
            {
                while (reader.Peek() >= 0)
                {
                    //Skip first line. It's the reference of the resource, not the resource itself
                    string resourceReference = reader.ReadLine();
                    string resourceString = reader.ReadLine();

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

        public IResourceCache GetImplementation(ResourceCacheType cacheType)
        {
            if (cacheType != ResourceCacheType.ABS)
                throw new NotSupportedException($"{nameof(ABSResourceCache)} does not support cache type '{cacheType}'.");
            return this;
        }

        public void Skipped(string sourceCache, string destinationCache)
        {
            BlockBlobClient sourceBlobClient = _containerClient.GetBlockBlobClient(GetBlobKey(sourceCache));

            if (!sourceBlobClient.Exists())
            {
                _logger.LogWarning("ABS blob not found for Skipped. SourceKey='{SourceKey}', BlobPath='{BlobPath}', Container='{Container}', DestinationKey='{DestinationKey}'",
                    sourceCache, GetBlobKey(sourceCache), _settings.BlobContainerName, destinationCache);
                return;
            }

            AppendBlobClient destinationBlobClient = _containerClient.GetAppendBlobClient(GetBlobKey(destinationCache));
            destinationBlobClient.CreateIfNotExists();

            try
            {
                using (Stream sourceStream = sourceBlobClient.OpenRead(true))
                using (Stream destinationStream = destinationBlobClient.OpenWrite(false))
                {
                    sourceStream.CopyTo(destinationStream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error when reading skipped ABS blob: Source Cache = {source}, Destination Cache = {destination}", sourceCache, destinationCache);
            }
        }

        public void Delete(List<string> cacheKeys)
        {
            foreach (var cacheKey in cacheKeys)
            {
                _containerClient.DeleteBlobIfExists(GetBlobKey(cacheKey));
                _containerClient.DeleteBlobIfExists(GetBlobIdsKey(cacheKey));
            }
        }
    }
}
