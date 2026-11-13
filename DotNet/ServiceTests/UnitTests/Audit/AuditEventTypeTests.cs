using LantanaGroup.Link.Shared.Application.Models;

namespace UnitTests.Audit
{
    [Trait("Category", "UnitTests")]
    public class AuditEventTypeTests
    {
        [Theory]
        [InlineData(AuditEventType.Create, 0)]
        [InlineData(AuditEventType.Update, 1)]
        [InlineData(AuditEventType.Delete, 2)]
        [InlineData(AuditEventType.Query, 3)]
        [InlineData(AuditEventType.Submit, 4)]
        [InlineData(AuditEventType.Restore, 5)]
        public void AuditEventType_ExistingValuesKeepTheirNumbers(AuditEventType action, int expected)
        {
            //the enum crosses Kafka between producers and Audit, so reordering it would
            //make in-flight messages from an older producer read as the wrong action
            Assert.Equal(expected, (int)action);
        }

        [Fact]
        public void AuditEventType_SoftDeleteIsAppendedLast()
        {
            var values = Enum.GetValues<AuditEventType>();

            Assert.Equal(6, (int)AuditEventType.SoftDelete);
            Assert.Equal(AuditEventType.SoftDelete, values[^1]);
        }

        [Fact]
        public void AuditEventType_SoftDeleteRendersAsSoftDeleteActionText()
        {
            //AuditModel.FromMessage stores Action via ToString()
            Assert.Equal("SoftDelete", AuditEventType.SoftDelete.ToString());
        }
    }
}
