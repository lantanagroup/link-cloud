using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Normalization.Application.Config
{
    public class BlobStorageSettings
    {
        public const string Key = "CacheBlobStorage";
        public string? ConnectionString { get; set; }
        public string? BlobContainerName { get; set; }
        public string? BlobRoot { get; set; }
    }
}
