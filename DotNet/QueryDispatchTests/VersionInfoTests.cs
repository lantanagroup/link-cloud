using LantanaGroup.Link.Shared.Application;
using Assert = Xunit.Assert;

namespace QueryDispatchUnitTests
{
    public class VersionInfoTests
    {

        [Fact]
        public async Task CanGetVersionInfo()
        {
            var versionInfo = await VersionInfo.Load();

            Assert.NotNull(versionInfo);
            Assert.True(versionInfo.VersionNumber.Length > 0);
            Assert.True(versionInfo.VersionName.Length > 0);
        }
    }
}
