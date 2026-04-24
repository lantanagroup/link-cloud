using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Humanizer.Localisation;
using LantanaGroup.Link.Normalization.Application.Config;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Normalization.Application.Models.Cache
{
    public class ABSResourceCache : IResourceCache
    {
        //private readonly BlobStorageService _blobStorageService;
        private readonly BlobContainerClient _containerClient;
        private readonly BlobStorageSettings _settings;

        public ABSResourceCache(IOptions<BlobStorageSettings> settings)
        {
            _settings = settings.Value;
            _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
        }

        public void UpdateCorrelationCache(string correlationId, List<DomainResource> resources, ResourceType resourceType, out string destination)
        {
            throw new NotImplementedException();
        }

        public void CopyResourcesToCorrelationCache(string sourceCache, string destinationCache)
        {
            //Nothing to do
        }

        public void Delete(List<string> cacheKeys)
        {
            //Nothing to do
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
                    reader.ReadLine();
                    string resourceString = reader.ReadLine();

                    DomainResource resource = JsonSerializer.Deserialize<DomainResource>(resourceString, LinkFhirSerializerOptions.ForFhirLenientSerialization);
                    resources.Add(resource);
                }
            }

            return resources;
        }

        public ResourceType GetResourceTypeByEventKey(string cacheKey)
        {
            throw new NotImplementedException();
        }
    }
}
