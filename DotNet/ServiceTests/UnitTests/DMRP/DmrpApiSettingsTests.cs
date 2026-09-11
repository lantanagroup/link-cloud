using LantanaGroup.Link.DMRP.Config;

namespace UnitTests.DMRP
{
    /// <summary>
    /// Covers the timeout a configured value resolves to.
    /// </summary>
    /// <remarks>
    /// The client is configured the first time it is created rather than at startup, so a value
    /// HttpClient refuses does not stop the service booting -- it fails the first refresh that
    /// tries to use it, which is a much worse place to find out.
    /// </remarks>
    [Trait("Category", "UnitTests")]
    public class DmrpApiSettingsTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(600)]
        public void AValueInRange_IsUsedAsGiven(int seconds)
        {
            var settings = new DmrpApiSettings { TimeoutSeconds = seconds };

            Assert.Equal(TimeSpan.FromSeconds(seconds), settings.ResolvedTimeout);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(601)]
        [InlineData(2147484)]
        [InlineData(int.MaxValue)]
        public void AValueOutOfRange_FallsBackToTheDefault(int seconds)
        {
            var settings = new DmrpApiSettings { TimeoutSeconds = seconds };

            // 2147484 seconds is past the point HttpClient.Timeout rejects the TimeSpan outright,
            // so honouring it would throw inside the client factory rather than merely wait too
            // long. Everything outside the range is treated the same way: as a value nobody could
            // have meant, answered with the one that works.
            Assert.Equal(TimeSpan.FromSeconds(30), settings.ResolvedTimeout);
        }

        [Fact]
        public void TheDefaultSettingsResolveWithoutConfiguration()
        {
            // The registration falls back to a bare settings object when DMRP:Api is absent.
            Assert.Equal(TimeSpan.FromSeconds(30), new DmrpApiSettings().ResolvedTimeout);
        }
    }
}
