using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace UnitTests.Shared.Auditing
{
    [Trait("Category", "UnitTests")]
    public class AuditContextTests
    {
        [Fact]
        public void ANewContext_CarriesNothing()
        {
            var context = new AuditContext();

            Assert.Null(context.FacilityId);
            Assert.Null(context.PatientId);
            Assert.Null(context.RecordId);
            Assert.Null(context.ActionOverride);
            Assert.False(context.SuppressNote);
            Assert.Empty(context.Values);
            Assert.Empty(context.PropertyChanges);
        }

        [Fact]
        public void Set_MakesTheValueAvailableToTheNotesTemplate()
        {
            var context = new AuditContext();

            context.Set("Version", "2.1");

            Assert.Equal("2.1", context.Values["Version"]);
        }

        [Fact]
        public void Set_IgnoresCaseSoAHandlerNeedNotMatchThePlaceholder()
        {
            var context = new AuditContext();

            context.Set("version", "2.1");

            Assert.Equal("2.1", context.Values["Version"]);
        }

        [Fact]
        public void Set_OverwritesAValueSetEarlierInTheRequest()
        {
            var context = new AuditContext();

            context.Set("Name", "first");
            context.Set("Name", "second");

            Assert.Equal("second", context.Values["Name"]);
        }

        [Fact]
        public void Set_AcceptsNullSoAHandlerCanRecordAnAbsentValue()
        {
            var context = new AuditContext();

            context.Set("Name", null);

            Assert.True(context.Values.ContainsKey("Name"));
            Assert.Null(context.Values["Name"]);
        }

        [Fact]
        public void TheHandlerAndTheInterceptor_ShareOnePropertyChangeList()
        {
            IAuditContext context = new AuditContext();

            context.PropertyChanges.Add(new PropertyChangeModel("Name", "old", "new"));

            Assert.Single(context.PropertyChanges);
            Assert.Equal("Name", context.PropertyChanges[0].PropertyName);
        }

        [Fact]
        public void ActionOverride_LetsACreateOrUpdateEndpointSayWhichItDid()
        {
            IAuditContext context = new AuditContext();

            context.ActionOverride = AuditEventType.Create;

            Assert.Equal(AuditEventType.Create, context.ActionOverride);
        }
    }
}
