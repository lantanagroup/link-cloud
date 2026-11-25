using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        public void AddBlobStorageSettings_ShippedAppSettingsWithoutConnectionStrings_FailsStartupValidation()
        {
            // The shipped appsettings leave both connection strings null for each environment to supply.
            using var provider = BuildProvider(new Dictionary<string, string?>());

            var exception = Assert.Throws<AggregateException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

            var failures = exception.InnerExceptions
                .Cast<OptionsValidationException>()
                .SelectMany(failure => failure.Failures)
                .ToList();
            Assert.Equal(2, failures.Count);
            Assert.Contains("InternalBlobStorage:ConnectionString is required.", failures);
            Assert.Contains("ExternalBlobStorage:ConnectionString is required.", failures);
        }

        [Fact]
        public void AddBlobStorageSettings_ConnectionStringsSupplied_PassesStartupValidation()
        {
            using var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["InternalBlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                ["ExternalBlobStorage:ConnectionString"] = "UseDevelopmentStorage=true"
            });

            provider.GetRequiredService<IStartupValidator>().Validate();
        }

        private static ServiceProvider BuildProvider(Dictionary<string, string?> overrides)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Resources", "Submission", "appsettings.json"))
                .AddInMemoryCollection(overrides)
                .Build();

            return new ServiceCollection()
                .AddBlobStorageSettings(configuration)
                .BuildServiceProvider();
        }
    }
}
