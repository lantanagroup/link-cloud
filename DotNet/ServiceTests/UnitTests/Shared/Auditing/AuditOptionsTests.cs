using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace UnitTests.Shared.Auditing
{
    [Trait("Category", "UnitTests")]
    public class AuditOptionsTests
    {
        [Theory]
        [InlineData("Password")]
        [InlineData("Secret")]
        [InlineData("ClientSecret")]
        [InlineData("Token")]
        [InlineData("AccessToken")]
        [InlineData("RefreshToken")]
        [InlineData("ApiKey")]
        [InlineData("PrivateKey")]
        [InlineData("Passphrase")]
        [InlineData("ConnectionString")]
        [InlineData("Authentication")]
        [InlineData("Credentials")]
        public void MaskedPropertyNames_CoversAuthenticationCredentialsAndSecretsByDefault(string name)
        {
            var options = new AuditOptions();

            Assert.Contains(name, options.MaskedPropertyNames);
        }

        [Fact]
        public void MaskedPropertyNames_MatchRegardlessOfCase()
        {
            var options = new AuditOptions();

            //the interceptor asks the collection itself, which is the comparison that matters
            Assert.True(options.MaskedPropertyNames.Contains("clientsecret"));
        }

        [Theory]
        [InlineData("*Secret*")]
        [InlineData("*Password*")]
        [InlineData("*Token*")]
        [InlineData("*Key")]
        public void MaskedPropertyPatterns_CoversTheSharedPatternsByDefault(string pattern)
        {
            var options = new AuditOptions();

            Assert.Contains(pattern, options.MaskedPropertyPatterns);
        }

        [Theory]
        [InlineData("ModifyDate")]
        [InlineData("ModifiedDate")]
        [InlineData("ModifiedOn")]
        [InlineData("LastModifiedOn")]
        [InlineData("UpdatedAt")]
        [InlineData("LastUpdatedDate")]
        [InlineData("LastUpdated")]
        public void TimestampPropertyNames_CoverTheNamesTheServicesUseByDefault(string name)
        {
            Assert.Contains(name, new AuditOptions().TimestampPropertyNames);
        }

        [Fact]
        public void TimestampPropertyNames_MatchRegardlessOfCase()
        {
            //the interceptor asks the collection itself, which is the comparison that matters
            Assert.True(new AuditOptions().TimestampPropertyNames.Contains("lastmodifiedon"));
        }

        [Fact]
        public void TimestampPropertyNames_CannotBeRemovedByAService()
        {
            var options = new AuditOptions();

            Assert.Throws<NotSupportedException>(() => options.TimestampPropertyNames.Remove("ModifyDate"));
        }

        [Fact]
        public void TimestampPropertyNames_AreNotSecrets()
        {
            //kept apart so the startup log never presents a timestamp as a sensitive field.
            Assert.DoesNotContain("LastModifiedOn", new AuditOptions().MaskedPropertyNames);
        }

        [Fact]
        public void AService_CanAddItsOwnPropertyName()
        {
            var options = new AuditOptions();

            //a sensitive name nothing shared catches
            options.MaskedPropertyNames.Add("SessionCookie");

            Assert.Contains("SessionCookie", options.MaskedPropertyNames);
        }

        [Fact]
        public void AddingTheSameEntryTwice_DoesNotDuplicateIt()
        {
            var options = new AuditOptions();

            options.MaskedPropertyNames.Add("Password");

            Assert.Single(options.MaskedPropertyNames, n => string.Equals(n, "Password", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void MaskedPropertyNames_CannotBeRemovedByAService()
        {
            var options = new AuditOptions();

            Assert.Throws<NotSupportedException>(() => options.MaskedPropertyNames.Remove("Password"));
        }

        [Fact]
        public void MaskedPropertyNames_CannotBeClearedByAService()
        {
            var options = new AuditOptions();

            Assert.Throws<NotSupportedException>(() => options.MaskedPropertyNames.Clear());
        }

        [Fact]
        public void MaskedPropertyPatterns_CannotBeRemovedByAService()
        {
            var options = new AuditOptions();

            Assert.Throws<NotSupportedException>(() => options.MaskedPropertyPatterns.Remove("*Secret*"));
        }
    }
}
