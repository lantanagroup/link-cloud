using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests.Submission
{
    [Trait("Category", "UnitTests")]
    public class BlobStorageServiceTests
    {
        [Theory]
        [InlineData("root", "facilityid_ach_20240101_abc", "manifest.ndjson", false, "root/facilityid_ach_20240101_abc/manifest.ndjson")]
        [InlineData("root", "facilityid_ach_20240101_abc", "manifest.ndjson", true,  "root/facilityid_ach_20240101_abc_manifest.ndjson")]
        [InlineData("root", "facilityid_ach_20240101_abc", "patient-p001.ndjson", false, "root/facilityid_ach_20240101_abc/patient-p001.ndjson")]
        [InlineData("root", "facilityid_ach_20240101_abc", "patient-p001.ndjson", true,  "root/facilityid_ach_20240101_abc_patient-p001.ndjson")]
        [InlineData(null,   "facilityid_ach_20240101_abc", "manifest.ndjson", false, "facilityid_ach_20240101_abc/manifest.ndjson")]
        [InlineData(null,   "facilityid_ach_20240101_abc", "manifest.ndjson", true,  "facilityid_ach_20240101_abc_manifest.ndjson")]
        public void GetExternalBlobName_ReturnsExpectedPath(
            string? blobRoot, string reportName, string bundleName, bool flatten, string expected)
        {
            var result = BlobStorageService.GetExternalBlobName(blobRoot, null, reportName, bundleName, flatten);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetExternalBlobName_FlattenFalse_ProducesThreeSlashSegments()
        {
            var result = BlobStorageService.GetExternalBlobName("root", null, "report", "bundle.ndjson", false);

            Assert.Equal(3, result.Split('/').Length);
            Assert.EndsWith("/bundle.ndjson", result);
        }

        [Fact]
        public void GetExternalBlobName_FlattenTrue_ProducesTwoSlashSegments()
        {
            var result = BlobStorageService.GetExternalBlobName("root", null, "report", "bundle.ndjson", true);

            Assert.Equal(2, result.Split('/').Length);
            Assert.EndsWith("_bundle.ndjson", result);
        }

        [Fact]
        public void Constructor_ShippedAppSettingsWithoutConnectionString_ThrowsArgumentException()
        {
            // The shipped nulls bind as empty strings on .NET 8, so the null check that skips an unconfigured
            // container is passed and the client rejects the empty container name. On .NET 10 they stay null
            // and the service starts with no containers; see ConfigurationNullBindingTests.
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Resources", "Submission", "appsettings.json"))
                .Build();
            var internalSettings = new InternalBlobStorageSettings();
            var externalSettings = new ExternalBlobStorageSettings();
            configuration.GetSection(InternalBlobStorageSettings.Key).Bind(internalSettings);
            configuration.GetSection(ExternalBlobStorageSettings.Key).Bind(externalSettings);

            Assert.Throws<ArgumentException>(() => new BlobStorageService(NullLogger<BlobStorageService>.Instance,
                                                                          Options.Create(internalSettings),
                                                                          Options.Create(externalSettings)));
        }
    }
}
