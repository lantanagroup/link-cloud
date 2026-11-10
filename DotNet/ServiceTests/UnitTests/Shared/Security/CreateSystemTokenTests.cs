using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Security
{
    /// <summary>
    /// LEGLINK-1359: which email a minted system token carries, and what else it must keep carrying
    /// regardless.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class CreateSystemTokenTests
    {
        private const string AdminEmail = "link-admin@example.org";

        //HmacSha512 wants a 512-bit key, so this is 64 characters rather than a token value.
        private const string SigningKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private sealed class StubActorEmailProvider(string? email) : IActorEmailProvider
        {
            public string? CurrentEmail { get; } = email;
        }

        private static CreateSystemToken AMinter(IActorEmailProvider? actorEmailProvider = null, bool logToken = false) =>
            new(NullLogger<CreateSystemToken>.Instance,
                Options.Create(new LinkTokenServiceSettings
                {
                    Authority = "https://link.example.org",
                    LinkAdminEmail = AdminEmail,
                    LogToken = logToken
                }),
                actorEmailProvider);

        private static async Task<JwtSecurityToken> AToken(IActorEmailProvider? actorEmailProvider = null) =>
            new JwtSecurityTokenHandler().ReadJwtToken(await AMinter(actorEmailProvider).ExecuteAsync(SigningKey, 5));

        private static string? EmailClaim(JwtSecurityToken token) =>
            token.Claims.FirstOrDefault(claim => claim.Type == LinkAuthorizationConstants.LinkSystemClaims.Email)?.Value;

        [Fact]
        public async Task WithNoProviderRegistered_TheTokenStillCarriesTheConfiguredAdminEmail()
        {
            //every Link service and worker: nothing registers a provider, so nothing about the token
            //they mint today may change.
            var token = await AToken();

            Assert.Equal(AdminEmail, EmailClaim(token));
        }

        [Fact]
        public async Task WithAProviderThatNamesSomeone_TheTokenCarriesThatAddressInsteadOfTheAdmins()
        {
            var token = await AToken(new StubActorEmailProvider("nurse@hospital.example.org"));

            Assert.Equal("nurse@hospital.example.org", EmailClaim(token));
            Assert.DoesNotContain(AdminEmail, token.Claims.Select(claim => claim.Value));
        }

        [Fact]
        public async Task WithAProviderThatNamesNobody_TheTokenCarriesNoEmailClaimAtAll()
        {
            //the point of the story: an un-attributable call records no actor rather than naming the
            //admin, who did nothing.
            var token = await AToken(new StubActorEmailProvider(null));

            Assert.Null(EmailClaim(token));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AProviderReturningBlank_IsTreatedAsNamingNobody(string blank)
        {
            var token = await AToken(new StubActorEmailProvider(blank));

            Assert.Null(EmailClaim(token));
        }

        [Fact]
        public async Task TheActorEmail_IsTrustedAsGivenAndNotRewritten()
        {
            //the provider is responsible for trimming; the minter must not quietly alter an address,
            //because the value is what a reader of the audit event sees.
            var token = await AToken(new StubActorEmailProvider("Mixed.Case@Hospital.Example.ORG"));

            Assert.Equal("Mixed.Case@Hospital.Example.ORG", EmailClaim(token));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("someone@hospital.example.org")]
        public async Task WhateverTheActor_TheTokensAuthorizationClaimsAreUnchanged(string? actorEmail)
        {
            //authorization reads subject, role and permissions - never the email - so attributing a
            //call must not change what the token is allowed to do.
            var token = await AToken(new StubActorEmailProvider(actorEmail));

            Assert.Equal(LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount,
                token.Claims.First(claim => claim.Type == LinkAuthorizationConstants.LinkSystemClaims.Subject).Value);
            Assert.Equal(LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor,
                token.Claims.First(claim => claim.Type == LinkAuthorizationConstants.LinkSystemClaims.Role).Value);
            Assert.Equal(nameof(LinkSystemPermissions.IsLinkAdmin),
                token.Claims.First(claim => claim.Type == LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions).Value);
        }

        [Fact]
        public async Task AProviderThatThrows_FailsTheCallRatherThanMintingAnUnattributedToken()
        {
            //a broken provider must not silently degrade to a token that names nobody: that would
            //record every call as un-attributed and look exactly like the legitimate case.
            var minter = AMinter(new ThrowingActorEmailProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() => minter.ExecuteAsync(SigningKey, 5));
        }

        [Fact]
        public async Task LogToken_RecordsMetadataRatherThanTheJwtOrTheActorsAddress()
        {
            //LEGLINK-1359 made this token carry the caller's address rather than the deployment's
            //admin one, and the activity event below is exported - so it must name neither the
            //address nor the credential that holds it. That an actor was named is the part worth
            //keeping: it is what makes an audit event with no User diagnosable, and it discloses
            //nobody.
            const string actorEmail = "nurse@hospital.example.org";
            using var activity = new Activity("CreateSystemTokenTests").Start();

            var jwt = await AMinter(new StubActorEmailProvider(actorEmail), logToken: true)
                .ExecuteAsync(SigningKey, 5);

            var logged = activity.Events.Single();
            Assert.Equal("Token generated.", logged.Name);
            Assert.All(logged.Tags, tag =>
            {
                var value = tag.Value?.ToString() ?? string.Empty;
                Assert.DoesNotContain(actorEmail, value);
                Assert.DoesNotContain(jwt, value);
            });

            var tags = logged.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
            Assert.Equal("https://link.example.org", tags["token.issuer"]?.ToString());
            Assert.True(tags["token.actor_email_present"] is true);
        }

        private sealed class ThrowingActorEmailProvider : IActorEmailProvider
        {
            public string? CurrentEmail => throw new InvalidOperationException("no context");
        }
    }
}
