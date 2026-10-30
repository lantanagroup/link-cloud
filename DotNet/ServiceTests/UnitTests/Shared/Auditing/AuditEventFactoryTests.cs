using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Shared.Auditing
{
    [Trait("Category", "UnitTests")]
    public class AuditEventFactoryTests
    {
        private static readonly AuditableAttribute UpdateFacility = new(AuditEventType.Update, "Facility");

        [Fact]
        public void User_IsTheCallersEmailClaim()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.SignedInAs("nurse@example.org"), [], new AuditContext());

            Assert.Equal("nurse@example.org", auditEvent.User);
        }

        [Fact]
        public void User_IsNullWhenTheTokenCarriesNoEmailClaim()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.SignedInWithoutEmail(), [], new AuditContext());

            //no SystemAccount, no "anonymous": a row naming an actor the token does not identify
            //would be a claim about who acted that nothing supports.
            Assert.Null(auditEvent.User);
        }

        [Fact]
        public void User_IsNullWhenNobodyIsSignedIn()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            Assert.Null(auditEvent.User);
        }

        [Fact]
        public void User_IsNullWhenThereIsNoPrincipalAtAll()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility, principal: null, [], new AuditContext());

            Assert.Null(auditEvent.User);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void User_IsNullWhenTheEmailClaimIsPresentButEmpty(string email)
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.SignedInAs(email), [], new AuditContext());

            Assert.Null(auditEvent.User);
        }

        [Fact]
        public void UserIdAndCorrelationId_AreNotSet()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.SignedInAs("nurse@example.org"), [], new AuditContext());

            Assert.Null(auditEvent.UserId);
            Assert.Null(auditEvent.CorrelationId);
        }

        [Fact]
        public void ServiceName_IsTheConfiguredShortNameNotTheDisplayName()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            Assert.Equal(AuditTest.ConfiguredServiceName, auditEvent.ServiceName);
        }

        [Fact]
        public void ServiceName_FallsBackToTheDisplayNameWhenNoConfigNameIsSet()
        {
            var service = new ServiceInformation { ServiceName = "Link Terminology Service" };

            var auditEvent = AuditTest.Factory(service: service).Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            Assert.Equal("Link Terminology Service", auditEvent.ServiceName);
        }

        [Fact]
        public void EventDate_IsWhenTheRequestWasAudited()
        {
            var now = new DateTimeOffset(2026, 10, 22, 14, 30, 0, TimeSpan.Zero);

            var auditEvent = AuditTest.Factory(new FakeTimeProvider(now)).Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            Assert.Equal(now.UtcDateTime, auditEvent.EventDate);
        }

        [Fact]
        public void Action_ComesFromTheAttribute()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            Assert.Equal(AuditEventType.Update, auditEvent.Action);
            Assert.Equal("Facility", auditEvent.Resource);
        }

        [Fact]
        public void Action_IsOverriddenWhenACreateOrUpdateEndpointSaysItCreated()
        {
            var context = new AuditContext { ActionOverride = AuditEventType.Create };

            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], context);

            Assert.Equal(AuditEventType.Create, auditEvent.Action);
        }

        [Fact]
        public void ResourceId_ComesFromTheRoutesIdByDefault()
        {
            var route = new RouteValueDictionary { ["id"] = "7c9e6679" };

            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), route, new AuditContext());

            Assert.Equal("7c9e6679", auditEvent.ResourceId);
        }

        [Fact]
        public void ResourceId_ComesFromTheRouteKeyTheAttributeNames()
        {
            var auditable = new AuditableAttribute(AuditEventType.SoftDelete, "Facility")
            {
                RecordIdRouteKey = "facilityId"
            };
            var route = new RouteValueDictionary { ["facilityId"] = "TestFacility01" };

            var auditEvent = AuditTest.Factory().Create(auditable,
                AuditTest.NobodySignedIn(), route, new AuditContext());

            Assert.Equal("TestFacility01", auditEvent.ResourceId);
        }

        [Fact]
        public void ResourceId_PrefersWhatTheHandlerSet()
        {
            var route = new RouteValueDictionary { ["id"] = "from-the-route" };
            var context = new AuditContext { RecordId = "from-the-handler" };

            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), route, context);

            Assert.Equal("from-the-handler", auditEvent.ResourceId);
        }

        [Fact]
        public void ResourceId_IsNullForABulkEndpointWithNoSingleRecord()
        {
            var auditable = new AuditableAttribute(AuditEventType.Delete, "ReportingPlan",
                "All reporting plans deleted");

            var auditEvent = AuditTest.Factory().Create(auditable,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            Assert.Null(auditEvent.ResourceId);
        }

        [Fact]
        public void FacilityId_ComesFromTheRouteWhenTheEndpointHasOne()
        {
            var route = new RouteValueDictionary { ["facilityId"] = "TestFacility01" };

            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), route, new AuditContext());

            Assert.Equal("TestFacility01", auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_PrefersWhatTheHandlerSetForABodyCarriedFacility()
        {
            var route = new RouteValueDictionary { ["facilityId"] = "from-the-route" };
            var context = new AuditContext { FacilityId = "from-the-body" };

            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), route, context);

            Assert.Equal("from-the-body", auditEvent.FacilityId);
        }

        private static QueryCollection Query(params (string Key, string[] Values)[] parameters) =>
            new(parameters.ToDictionary(p => p.Key, p => new StringValues(p.Values)));

        [Fact]
        public void FacilityId_ComesFromTheRouteKeyTheAttributeNames()
        {
            //Data Acquisition's SFTP routes call the facility {organizationId}.
            var auditable = new AuditableAttribute(AuditEventType.Update, "SftpConfiguration")
            {
                FacilityIdRouteKey = "organizationId"
            };
            var route = new RouteValueDictionary { ["organizationId"] = "TestFacility01" };

            var auditEvent = AuditTest.Factory().Create(auditable,
                AuditTest.NobodySignedIn(), route, new AuditContext());

            Assert.Equal("TestFacility01", auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_ComesFromTheQueryStringWhenTheRouteHasNone()
        {
            //Normalization's OperationSequence endpoints take ?facilityId=.
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext(), Query(("facilityId", ["TestFacility01"])));

            Assert.Equal("TestFacility01", auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_QueryStringIsReadUnderTheKeyTheAttributeNames()
        {
            var auditable = new AuditableAttribute(AuditEventType.Update, "SftpConfiguration")
            {
                FacilityIdRouteKey = "organizationId"
            };

            var auditEvent = AuditTest.Factory().Create(auditable, AuditTest.NobodySignedIn(), [],
                new AuditContext(), Query(("organizationId", ["TestFacility01"]), ("facilityId", ["not-this-one"])));

            Assert.Equal("TestFacility01", auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_PrefersTheRouteOverTheQueryString()
        {
            var route = new RouteValueDictionary { ["facilityId"] = "from-the-route" };

            var auditEvent = AuditTest.Factory().Create(UpdateFacility, AuditTest.NobodySignedIn(), route,
                new AuditContext(), Query(("facilityId", ["from-the-query"])));

            Assert.Equal("from-the-route", auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_PrefersWhatTheHandlerSetOverTheQueryString()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility, AuditTest.NobodySignedIn(), [],
                new AuditContext { FacilityId = "from-the-body" }, Query(("facilityId", ["from-the-query"])));

            Assert.Equal("from-the-body", auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_IsNotGuessedWhenTheQueryStringRepeatsIt()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility, AuditTest.NobodySignedIn(), [],
                new AuditContext(), Query(("facilityId", ["first", "second"])));

            Assert.Null(auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_FromTheQueryStringIsSanitised()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility, AuditTest.NobodySignedIn(), [],
                new AuditContext(), Query(("facilityId", ["Test<script>alert(1)</script>Facility01"])));

            Assert.Equal("TestFacility01", auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_FromTheQueryStringThatSanitisesToNothing_IsNull()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility, AuditTest.NobodySignedIn(), [],
                new AuditContext(), Query(("facilityId", ["<script></script>"])));

            Assert.Null(auditEvent.FacilityId);
        }

        [Fact]
        public void FacilityId_MissingIsLoggedAtDebug_NamingTheKeyLookedForAndTheRoutesKeys()
        {
            //the SFTP routes call the facility {organizationId}: an attribute that forgets to say so
            //publishes events with no facility, and this line is the only place that shows up.
            var auditable = new AuditableAttribute(AuditEventType.Update, "SftpConfiguration");
            var route = new RouteValueDictionary { ["organizationId"] = "TestFacility01", ["configurationId"] = "42" };
            var logger = new CapturingLogger<AuditEventFactory>();

            var auditEvent = AuditTest.Factory(logger: logger).Create(auditable,
                AuditTest.NobodySignedIn(), route, new AuditContext());

            Assert.Null(auditEvent.FacilityId);
            var entry = Assert.Single(logger.Entries, e => e.Message.Contains("no FacilityId"));
            Assert.Equal(LogLevel.Debug, entry.Level);
            Assert.Contains("'facilityId'", entry.Message);
            Assert.Contains("organizationId", entry.Message);
        }

        [Fact]
        public void FacilityId_Found_IsNotLogged()
        {
            var route = new RouteValueDictionary { ["facilityId"] = "TestFacility01" };
            var logger = new CapturingLogger<AuditEventFactory>();

            AuditTest.Factory(logger: logger).Create(UpdateFacility, AuditTest.NobodySignedIn(), route, new AuditContext());

            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("no FacilityId"));
        }

        [Fact]
        public void FacilityId_SetByTheHandler_IsNotLogged()
        {
            var logger = new CapturingLogger<AuditEventFactory>();

            AuditTest.Factory(logger: logger).Create(UpdateFacility, AuditTest.NobodySignedIn(), [],
                new AuditContext { FacilityId = "from-the-body" });

            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("no FacilityId"));
        }

        [Fact]
        public void FacilityId_Missing_IsNotLoggedAboveDebug()
        {
            //an endpoint with no facility at all - HSLOC, Vendor - is normal, so production logs
            //at Information must not carry a line for every one of its events.
            var logger = new CapturingLogger<AuditEventFactory>(LogLevel.Information);

            AuditTest.Factory(logger: logger).Create(UpdateFacility, AuditTest.NobodySignedIn(), [], new AuditContext());

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void FacilityIdRouteKey_DefaultsToFacilityId()
        {
            Assert.Equal("facilityId", UpdateFacility.FacilityIdRouteKey);
        }

        [Fact]
        public void PatientId_IsWhateverTheHandlerRecorded()
        {
            var context = new AuditContext { PatientId = "Patient/123" };

            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], context);

            Assert.Equal("Patient/123", auditEvent.PatientId);
        }

        [Fact]
        public void Notes_AreAbsentUnlessTheAttributeGivesATemplate()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            //the ADR keeps notes off config events to hold the row size down.
            Assert.Null(auditEvent.Notes);
        }

        [Fact]
        public void Notes_FillPlaceholdersFromTheRoute()
        {
            var auditable = new AuditableAttribute(AuditEventType.Delete, "HSLOC",
                "All HSLOC codes for version {version} deleted");
            var route = new RouteValueDictionary { ["version"] = "2024.1" };

            var auditEvent = AuditTest.Factory().Create(auditable,
                AuditTest.NobodySignedIn(), route, new AuditContext());

            Assert.Equal("All HSLOC codes for version 2024.1 deleted", auditEvent.Notes);
        }

        [Fact]
        public void Notes_FillPlaceholdersFromWhatTheHandlerRecorded()
        {
            var auditable = new AuditableAttribute(AuditEventType.Delete, "VendorVersion",
                "Id {id} deleted (Vendor: {Name}, Version: {Version})");
            var route = new RouteValueDictionary { ["id"] = "42" };
            var context = new AuditContext();
            context.Set("Name", "Epic");
            context.Set("Version", "2024.1");

            var auditEvent = AuditTest.Factory().Create(auditable,
                AuditTest.NobodySignedIn(), route, context);

            Assert.Equal("Id 42 deleted (Vendor: Epic, Version: 2024.1)", auditEvent.Notes);
        }

        [Fact]
        public void Notes_LeaveAnUnknownPlaceholderVisible()
        {
            var auditable = new AuditableAttribute(AuditEventType.Delete, "HSLOC",
                "All HSLOC codes for version {version} deleted");

            var auditEvent = AuditTest.Factory().Create(auditable,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            //a note that quietly read as prose would hide that the value was never supplied.
            Assert.Equal("All HSLOC codes for version {version} deleted", auditEvent.Notes);
        }

        [Fact]
        public void Notes_AreDroppedWhenTheHandlerSuppressesThem()
        {
            var auditable = new AuditableAttribute(AuditEventType.Update, "Artifact",
                "Artifact (Type: {type}, Name: {name}) created");
            var context = new AuditContext { SuppressNote = true };

            var auditEvent = AuditTest.Factory().Create(auditable,
                AuditTest.NobodySignedIn(), [], context);

            Assert.Null(auditEvent.Notes);
        }

        [Fact]
        public void PropertyChanges_AreAbsentWhenNothingChanged()
        {
            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], new AuditContext());

            //an empty list and no list read the same downstream; absent keeps the row smaller.
            Assert.Null(auditEvent.PropertyChanges);
        }

        [Fact]
        public void PropertyChanges_AreCarriedWhenTheInterceptorFoundSome()
        {
            var context = new AuditContext();
            context.PropertyChanges.Add(new("Name", "Before", "After"));

            var auditEvent = AuditTest.Factory().Create(UpdateFacility,
                AuditTest.NobodySignedIn(), [], context);

            Assert.NotNull(auditEvent.PropertyChanges);
            var change = Assert.Single(auditEvent.PropertyChanges);
            Assert.Equal("Name", change.PropertyName);
            Assert.Equal("Before", change.InitialPropertyValue);
            Assert.Equal("After", change.NewPropertyValue);
        }

        [Theory]
        [InlineData(AuditEventType.Create)]
        [InlineData(AuditEventType.Delete)]
        [InlineData(AuditEventType.SoftDelete)]
        [InlineData(AuditEventType.Restore)]
        [InlineData(AuditEventType.Query)]
        [InlineData(AuditEventType.Submit)]
        public void PropertyChanges_AreLeftOffEveryActionButUpdate(AuditEventType action)
        {
            //a soft delete is saved as a modification, so the interceptor finds IsDeleted changed on
            //every row - one entry per schedule on Report's facility-wide status endpoint.
            var context = new AuditContext();
            context.PropertyChanges.Add(new("IsDeleted", "False", "True"));

            var auditEvent = AuditTest.Factory().Create(new AuditableAttribute(action, "ReportSchedule"),
                AuditTest.NobodySignedIn(), [], context);

            Assert.Null(auditEvent.PropertyChanges);
        }

        [Fact]
        public void PropertyChanges_AreLeftOffAnUpdateWhoseEndpointSuppressesThem()
        {
            //Normalization's HSLOC upload moves every code to the new version in one request.
            var auditable = new AuditableAttribute(AuditEventType.Update, "HSLOC",
                "HSLOC updated from version {oldVersion} to {newVersion}")
            {
                SuppressPropertyChanges = true
            };
            var context = new AuditContext();
            context.PropertyChanges.Add(new("Version", "2024", "2025"));
            context.Set("oldVersion", "2024");
            context.Set("newVersion", "2025");

            var auditEvent = AuditTest.Factory().Create(auditable, AuditTest.NobodySignedIn(), [], context);

            Assert.Null(auditEvent.PropertyChanges);
            Assert.Equal("HSLOC updated from version 2024 to 2025", auditEvent.Notes);
        }

        [Fact]
        public void PropertyChanges_AreNotSuppressedByDefault()
        {
            Assert.False(UpdateFacility.SuppressPropertyChanges);
        }

        [Fact]
        public void PropertyChanges_FollowTheActionTheHandlerOverrodeTo()
        {
            //QueryDispatch's PUT creates the configuration when none exists and says so.
            var created = new AuditContext { ActionOverride = AuditEventType.Create };
            created.PropertyChanges.Add(new("Name", "Before", "After"));
            var updated = new AuditContext { ActionOverride = AuditEventType.Update };
            updated.PropertyChanges.Add(new("Name", "Before", "After"));

            var createdEvent = AuditTest.Factory().Create(UpdateFacility, AuditTest.NobodySignedIn(), [], created);
            var updatedEvent = AuditTest.Factory().Create(new AuditableAttribute(AuditEventType.Create, "Facility"),
                AuditTest.NobodySignedIn(), [], updated);

            Assert.Null(createdEvent.PropertyChanges);
            Assert.NotNull(updatedEvent.PropertyChanges);
        }

        [Fact]
        public void AnEmptyResource_IsRejectedWhereTheDeveloperWillSeeIt()
        {
            Assert.Throws<ArgumentException>(() => new AuditableAttribute(AuditEventType.Update, "  "));
        }
    }
}
