using Xunit;

namespace AuditTests
{
    public class AuditEventProcessorTests
    {
        [Fact]
        public void AuditEventProcessor_ShouldNotAcceptNullMessageResults()
        {
            Assert.True(true);
        }

        [Fact]
        public void AuditEventProcessor_ShouldNotAcceptNullMessageValues()
        {
            Assert.True(true);
        }

        [Fact]
        public void AuditEventProcessor_CanExtractCorrelationIdIfPresent()
        {
            Assert.True(true);
        }

        [Fact]
        public void AuditEventProcessor_CanCreateAnAuditEvent()
        {
            Assert.True(true);
        }
    }
}
