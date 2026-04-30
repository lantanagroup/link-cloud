using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;
using SharpCompress.Common;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    public class ABSResourceCache : IResourceCache
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ResourceCacheBlobStorageSettings _settings;

        public ABSResourceCache(IOptions<ResourceCacheBlobStorageSettings> settings)
        {
            _settings = settings.Value;
            _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
        }

        public void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType)
        {
            string blobName = _settings.BlobRoot + "/" + correlationId + "/" + resourceType.ToString();

            AppendBlobClient writeBlobClient = _containerClient.GetAppendBlobClient(blobName);

            using (Stream write_stream = writeBlobClient.OpenWrite(true))
            using (StreamWriter writer = new StreamWriter(write_stream)) 
            {
                foreach (var resource in resources) 
                {
                    writer.WriteLine(resource.TypeName + "/" + resource.Id);
                    writer.WriteLine(resource.ToJson());
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

            BlockBlobClient readBlobClient = _containerClient.GetBlockBlobClient(cacheKey);

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
                        throw new Exception($"Failed to deserialize FHIR DomainResource for the following ABS entry: " + resourceReference);
                    }
                }
            }

            return resources;
        }

        public ResourceType GetResourceTypeByCacheKey(string cacheKey)
        {
            string[] splitKey = cacheKey.Split("/");

            if (Enum.TryParse<ResourceType>(splitKey.Last(), out var resourceType))
            {
                return resourceType;
            }
            else
            {
                throw new Exception($"Could not parse the ABS cache key '{cacheKey}' into a valid FHIR Resource Type");
            }
        }

        public void Skipped(string sourceCache, string destinationCache)
        {
            //Nothing to do
        }

        public void Delete(List<string> cacheKeys)
        {
            //Nothing to do
        }
    }
}
