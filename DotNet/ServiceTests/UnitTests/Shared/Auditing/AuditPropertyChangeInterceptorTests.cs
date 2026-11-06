using System.Globalization;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    [Trait("Category", "UnitTests")]
    public class AuditPropertyChangeInterceptorTests
    {
        private static (AuditInterceptorFixture Fixture, AuditContext Audit) Build(
            Action<AuditOptions>? configure = null)
        {
            var audit = new AuditContext { IsRecording = true };
            var interceptor = new AuditPropertyChangeInterceptor(audit,
                AuditTest.Options(configure).AsOptions(),
                NullLogger<AuditPropertyChangeInterceptor>.Instance);

            return (new AuditInterceptorFixture(interceptor), audit);
        }

        private static TestVendor ASavedVendor(AuditInterceptorFixture fixture)
        {
            var vendor = new TestVendor
            {
                Name = "Epic",
                ClientSecret = "the-old-secret",
                SigningKey = "the-old-key",
                ContactEmail = "admin@example.org"
            };

            fixture.Context.Vendors.Add(vendor);
            fixture.Context.SaveChanges();

            return vendor;
        }

        [Fact]
        public async Task AnUpdate_RecordsTheOldAndNewValueOfWhatChanged()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();

            var change = Assert.Single(audit.PropertyChanges);
            Assert.Equal(nameof(TestVendor.Name), change.PropertyName);
            Assert.Equal("Epic", change.InitialPropertyValue);
            Assert.Equal("Cerner", change.NewPropertyValue);
        }

        [Fact]
        public async Task ASaveOutsideAnAuditedRequest_CapturesNothing()
        {
            //a Kafka listener or a Quartz job saving through the same context: no filter began, so
            //nothing will ever read what the interceptor would capture.
            var audit = new AuditContext();
            var interceptor = new AuditPropertyChangeInterceptor(audit, AuditTest.Options().AsOptions(),
                NullLogger<AuditPropertyChangeInterceptor>.Instance);
            using var fixture = new AuditInterceptorFixture(interceptor);
            var vendor = ASavedVendor(fixture);

            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task ACreate_RecordsNoPropertyChanges()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;

            fixture.Context.Vendors.Add(new TestVendor { Name = "Epic" });
            await fixture.Context.SaveChangesAsync();

            //there is no previous value to compare a new row against.
            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task ADelete_RecordsNoPropertyChanges()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            fixture.Context.Vendors.Remove(vendor);
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task APropertyAssignedItsOwnValue_IsNotAChange()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            //EF marks a property modified when it is assigned, whether or not the value differs.
            vendor.Name = "Epic";
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        //the wire contract for a masked value: a reader learns the property changed, never from
        //what or to what.
        private const string Masked = "[redacted]";

        private static void AssertMasked(AuditContext audit, string propertyName)
        {
            var change = Assert.Single(audit.PropertyChanges, c => c.PropertyName == propertyName);
            Assert.Equal(Masked, change.InitialPropertyValue);
            Assert.Equal(Masked, change.NewPropertyValue);
        }

        [Fact]
        public async Task ASecretNamedInTheSharedList_IsMasked()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            vendor.Name = "Cerner";
            vendor.ClientSecret = "the-new-secret";
            await fixture.Context.SaveChangesAsync();

            Assert.Equal(2, audit.PropertyChanges.Count);
            Assert.Single(audit.PropertyChanges, c => c.PropertyName == nameof(TestVendor.Name) && c.NewPropertyValue == "Cerner");
            AssertMasked(audit, nameof(TestVendor.ClientSecret));
            Assert.DoesNotContain(audit.PropertyChanges, c =>
                c.InitialPropertyValue.Contains("secret") || c.NewPropertyValue.Contains("secret"));
        }

        [Fact]
        public async Task APropertyCaughtOnlyByAPattern_IsMasked()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            //SigningKey is in no list; the *Key pattern is what catches it.
            vendor.SigningKey = "the-new-key";
            await fixture.Context.SaveChangesAsync();

            AssertMasked(audit, nameof(TestVendor.SigningKey));
        }

        [Fact]
        public async Task APropertyMarkedAuditMask_IsMasked()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            //personal data is no kind of secret, so only the attribute catches it.
            vendor.ContactEmail = "someone.else@example.org";
            await fixture.Context.SaveChangesAsync();

            AssertMasked(audit, nameof(TestVendor.ContactEmail));
        }

        [Fact]
        public async Task ANameAServiceAdds_IsMasked()
        {
            var (fixture, audit) = Build(options => options.MaskedPropertyNames.Add(nameof(TestVendor.Name)));
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();

            AssertMasked(audit, nameof(TestVendor.Name));
        }

        [Fact]
        public async Task AClearedSecret_IsMaskedOnBothSides()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            //an empty side would say the secret was removed - or, the other way round, first set.
            vendor.ClientSecret = string.Empty;
            await fixture.Context.SaveChangesAsync();

            AssertMasked(audit, nameof(TestVendor.ClientSecret));
        }

        [Fact]
        public async Task AMaskedPropertyAssignedItsOwnValue_IsNotAChange()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            vendor.ClientSecret = "the-old-secret";
            await fixture.Context.SaveChangesAsync();

            //a mask must not report a rotation that never happened.
            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task TheModifyTimestamp_IsNotReportedAsAChange()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            //UpdateBaseEntityInterceptor writes this on every save, so it would appear every time.
            vendor.Name = "Cerner";
            vendor.ModifyDate = DateTime.UtcNow;
            await fixture.Context.SaveChangesAsync();

            Assert.Equal([nameof(TestVendor.Name)], audit.PropertyChanges.Select(c => c.PropertyName));
        }

        private static TestAccountUser ASavedAccountUser(AuditInterceptorFixture fixture)
        {
            var user = new TestAccountUser();
            fixture.Context.AccountUsers.Add(user);
            fixture.Context.SaveChanges();
            return user;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ATimestampUnderAnotherServicesName_IsNotReportedAsAChange(bool alsoChangeSomething)
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var user = ASavedAccountUser(fixture);

            //Account's interceptor stamps LastModifiedOn on every save.
            if (alsoChangeSomething)
            {
                user.IsActive = false;
            }
            user.LastModifiedOn = DateTime.UtcNow;
            await fixture.Context.SaveChangesAsync();

            //a save that only moved the timestamp changed nothing anyone asked for, and says so.
            Assert.Equal(alsoChangeSomething ? [nameof(TestAccountUser.IsActive)] : [],
                audit.PropertyChanges.Select(c => c.PropertyName));
        }

        [Fact]
        public async Task ATimestampNameAServiceAdds_IsNotReportedAsAChange()
        {
            var (fixture, audit) = Build(options => options.TimestampPropertyNames.Add("StampedAt"));
            using var _ = fixture;
            var user = ASavedAccountUser(fixture);

            user.IsActive = false;
            user.StampedAt = DateTime.UtcNow;
            await fixture.Context.SaveChangesAsync();

            Assert.Equal([nameof(TestAccountUser.IsActive)], audit.PropertyChanges.Select(c => c.PropertyName));
        }

        private static TestDispatchConfiguration ASavedDispatchConfiguration(AuditInterceptorFixture fixture)
        {
            var configuration = new TestDispatchConfiguration
            {
                Schedules = [new TestSchedule { Event = "Discharge", Duration = "PT8H" }]
            };
            fixture.Context.DispatchConfigurations.Add(configuration);
            fixture.Context.SaveChanges();
            return configuration;
        }

        [Fact]
        public async Task AValueConvertedProperty_IsDescribedInTheFormItIsStored()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var configuration = ASavedDispatchConfiguration(fixture);

            //what QueryDispatch does: replace the whole list.
            configuration.Schedules = [new TestSchedule { Event = "Discharge", Duration = "PT12H" }];
            await fixture.Context.SaveChangesAsync();

            var change = Assert.Single(audit.PropertyChanges);
            Assert.Equal(nameof(TestDispatchConfiguration.Schedules), change.PropertyName);
            Assert.Equal("""[{"Event":"Discharge","Duration":"PT8H"}]""", change.InitialPropertyValue);
            Assert.Equal("""[{"Event":"Discharge","Duration":"PT12H"}]""", change.NewPropertyValue);
        }

        [Fact]
        public async Task AValueConvertedPropertyReplacedWithAnEqualValue_IsNotAChange()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var configuration = ASavedDispatchConfiguration(fixture);

            //a new list holding the same schedules: a different object, but nothing changed.
            configuration.Schedules = [new TestSchedule { Event = "Discharge", Duration = "PT8H" }];
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task CredentialsStoredAsJsonUnderAMaskedName_AreMasked()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var configuration = ASavedDispatchConfiguration(fixture);
            configuration.Authentication = new TestCredentials { AuthType = "Basic", ClientId = "link", Key = "old-key-value" };
            await fixture.Context.SaveChangesAsync();
            audit.PropertyChanges.Clear();

            //rotating the key, with a schedule change alongside so the save is not empty.
            configuration.Authentication = new TestCredentials { AuthType = "Basic", ClientId = "link", Key = "new-key-value" };
            configuration.Schedules = [new TestSchedule { Event = "Discharge", Duration = "PT12H" }];
            await fixture.Context.SaveChangesAsync();

            Assert.Equal(2, audit.PropertyChanges.Count);
            AssertMasked(audit, nameof(TestDispatchConfiguration.Authentication));
            Assert.DoesNotContain(audit.PropertyChanges, c =>
                c.InitialPropertyValue.Contains("key-value") || c.NewPropertyValue.Contains("key-value"));
        }

        [Fact]
        public async Task CredentialsStoredAsJsonReplacedWithEqualOnes_AreNotAChange()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var configuration = ASavedDispatchConfiguration(fixture);
            configuration.Authentication = new TestCredentials { AuthType = "Basic", ClientId = "link", Key = "same-key" };
            await fixture.Context.SaveChangesAsync();
            audit.PropertyChanges.Clear();

            //a PUT resending the same settings: Data Acquisition builds a new object every time.
            configuration.Authentication = new TestCredentials { AuthType = "Basic", ClientId = "link", Key = "same-key" };
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task AForeignKey_IsNotReportedAsAChange()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var tenant = new TestTenant { Name = "Example Health" };
            fixture.Context.Tenants.Add(tenant);
            var vendor = ASavedVendor(fixture);
            await fixture.Context.SaveChangesAsync();
            audit.PropertyChanges.Clear();

            //a key identifies the row rather than describing it.
            vendor.TenantId = tenant.Id;
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task TheChangedRecordsKey_NeverBecomesTheResourceId()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);

            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();

            var auditEvent = AuditTest.Factory().Create(new AuditableAttribute(AuditEventType.Update, "Vendor"),
                AuditTest.NobodySignedIn(), [], audit);

            //the first row a save touched may be a child, or one of several, so its key is not
            //evidence of which record the request was about. A missing id is visible; a wrong one
            //is not.
            Assert.NotEmpty(audit.PropertyChanges);
            Assert.Null(auditEvent.ResourceId);
        }

        [Fact]
        public async Task WhatTheHandlerSaidTheRecordWas_IsNotOverwritten()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.RecordId = "the-handler-knows-better";

            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();

            Assert.Equal("the-handler-knows-better", audit.RecordId);
        }

        [Fact]
        public async Task AnUntrackedUpdate_ProducesNoPropertyChanges()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var vendor = ASavedVendor(fixture);
            audit.PropertyChanges.Clear();

            //a known limit worth pinning down: an AsNoTracking load has no original values, so
            //there is nothing to diff and the event carries no changes.
            await fixture.Context.Vendors.Where(v => v.Id == vendor.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.Name, "Cerner"));

            Assert.Empty(audit.PropertyChanges);
        }

        private static TestFhirQuery ASavedQuery(AuditInterceptorFixture fixture,
            Action<TestFhirQuery>? initialise = null)
        {
            var query = new TestFhirQuery { QueryParameters = ["patient=1", "_count=50"] };
            initialise?.Invoke(query);

            fixture.Context.FhirQueries.Add(query);
            fixture.Context.SaveChanges();

            return query;
        }

        [Fact]
        public async Task ACollectionMappedWithNoConverter_IsDescribedByItsContents()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var query = ASavedQuery(fixture);
            audit.PropertyChanges.Clear();

            query.QueryParameters = ["patient=1", "_count=100"];
            await fixture.Context.SaveChangesAsync();

            //the type name would read the same on both sides and tell a reader nothing.
            var change = Assert.Single(audit.PropertyChanges);
            Assert.Equal(nameof(TestFhirQuery.QueryParameters), change.PropertyName);
            Assert.Equal("[\"patient=1\",\"_count=50\"]", change.InitialPropertyValue);
            Assert.Equal("[\"patient=1\",\"_count=100\"]", change.NewPropertyValue);
        }

        [Fact]
        public async Task ACollectionReplacedWithTheSameItems_IsNotReportedAsAChange()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var query = ASavedQuery(fixture);
            audit.PropertyChanges.Clear();

            //a different list object holding what the old one held: Equals calls it a change, the
            //described value does not.
            query.QueryParameters = ["patient=1", "_count=50"];
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task ABinaryValue_IsDescribedAsBase64RatherThanItsType()
        {
            var (fixture, audit) = Build();
            using var _ = fixture;
            var query = ASavedQuery(fixture, q => q.Fingerprint = [1, 2, 3]);
            audit.PropertyChanges.Clear();

            query.Fingerprint = [4, 5, 6];
            await fixture.Context.SaveChangesAsync();

            //base64 is what System.Text.Json does with a byte[] of its own accord, so there is no
            //arm for it - it is described as any other collection is. Asserted by containment
            //because that leaves the JSON string quoting out of it.
            var change = Assert.Single(audit.PropertyChanges);
            Assert.Equal(nameof(TestFhirQuery.Fingerprint), change.PropertyName);
            Assert.Contains(Convert.ToBase64String([1, 2, 3]), change.InitialPropertyValue);
            Assert.Contains(Convert.ToBase64String([4, 5, 6]), change.NewPropertyValue);
        }

        [Fact]
        public async Task AFormattableValue_ReadsTheSameWhateverTheHostsCulture()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                //a culture that writes the decimal separator as a comma and dates day-first, so a
                //value formatted for the host would not read the same on another one.
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                var (fixture, audit) = Build();
                using var _ = fixture;
                var query = ASavedQuery(fixture, q =>
                {
                    q.Threshold = 1.5m;
                    q.EffectiveFrom = new DateTime(2026, 10, 29, 13, 45, 0, DateTimeKind.Utc);
                });
                audit.PropertyChanges.Clear();

                query.Threshold = 2.75m;
                query.EffectiveFrom = new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc);
                await fixture.Context.SaveChangesAsync();

                var changes = audit.PropertyChanges.ToDictionary(c => c.PropertyName);

                Assert.Equal("1.5", changes[nameof(TestFhirQuery.Threshold)].InitialPropertyValue);
                Assert.Equal("2.75", changes[nameof(TestFhirQuery.Threshold)].NewPropertyValue);

                Assert.Equal("2026-10-29T13:45:00.0000000Z",
                    changes[nameof(TestFhirQuery.EffectiveFrom)].InitialPropertyValue);
                Assert.Equal("2026-11-01T06:00:00.0000000Z",
                    changes[nameof(TestFhirQuery.EffectiveFrom)].NewPropertyValue);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }
    }
}
