using LantanaGroup.Link.Submission.Application.Services;

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
    }
}
