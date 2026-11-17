using System.Security.Claims;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Session;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Claim = System.Security.Claims.Claim;

namespace UnitTests.Nhsn
{
    /// <summary>
    /// LEGLINK-1359: the email the BFF puts on the token it mints for a Link service, read from the
    /// gateway JWT on the request in flight.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class NhsnActorEmailProviderTests
    {
        private static NhsnActorEmailProvider AProvider(HttpContext? httpContext,
            NhsnJwtSettings? settings = null)
        {
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            return new NhsnActorEmailProvider(accessor, Options.Create(settings ?? new NhsnJwtSettings()));
        }

        private static HttpContext ARequestFrom(params Claim[] claims) =>
            new DefaultHttpContext
            {
                //an authentication type is what makes the identity authenticated.
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "NhsnGateway"))
            };

        [Fact]
        public void WithNoRequestInScope_NamesNobody()
        {
            //the SDK clients are singletons and resolve this once, so it is read on calls that may
            //have no request at all. That must be null, not a throw.
            Assert.Null(AProvider(httpContext: null).CurrentEmail);
        }

        [Fact]
        public void OnAnAuthenticatedRequest_NamesTheUpnClaim()
        {
            var provider = AProvider(ARequestFrom(new Claim("upn", "nurse@hospital.example.org")));

            Assert.Equal("nurse@hospital.example.org", provider.CurrentEmail);
        }

        [Fact]
        public void TheClaimTypeComesFromSettings_NotAHardCodedUpn()
        {
            var provider = AProvider(
                ARequestFrom(new Claim("preferred_email", "nurse@hospital.example.org")),
                new NhsnJwtSettings { EmailClaimType = "preferred_email" });

            Assert.Equal("nurse@hospital.example.org", provider.CurrentEmail);
        }

        [Theory]
        [InlineData("email")]
        [InlineData(ClaimTypes.Email)]
        public void WithoutTheConfiguredClaim_FallsBackToTheStandardEmailClaim(string claimType)
        {
            //the raw name is the one that can actually fire. Program.cs sets MapInboundClaims =
            //false, so an issuer's "email" claim keeps that name and is never rewritten to the
            //WS-Federation URI; matching only the URI made this fallback dead for any real token.
            var provider = AProvider(ARequestFrom(new Claim(claimType, "nurse@hospital.example.org")));

            Assert.Equal("nurse@hospital.example.org", provider.CurrentEmail);
        }

        [Fact]
        public void TheConfiguredClaimWins_WhenATokenCarriesBothNames()
        {
            //widening the fallback must not let a second claim outrank the one the environment
            //configured, since that is the row an operator changes to correct a gateway mismatch.
            var provider = AProvider(ARequestFrom(
                new Claim("upn", "configured@hospital.example.org"),
                new Claim("email", "standard@hospital.example.org")));

            Assert.Equal("configured@hospital.example.org", provider.CurrentEmail);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ABlankConfiguredClaim_DoesNotHideAUsableFallback(string blank)
        {
            //selection used to stop at the first claim that merely existed, so a gateway sending an
            //empty upn beside a real email claim named nobody: the token went out with no email at
            //all and the receiving service recorded the change against no one, even though the
            //address was right there. Nothing rejects a blank claim value, so this is a token a
            //gateway can actually send.
            var provider = AProvider(ARequestFrom(
                new Claim("upn", blank),
                new Claim("email", "standard@hospital.example.org")));

            Assert.Equal("standard@hospital.example.org", provider.CurrentEmail);
        }

        [Fact]
        public void BlanksAreSkippedRightAlongTheOrder_NotOnlyOnTheConfiguredClaim()
        {
            var provider = AProvider(ARequestFrom(
                new Claim("upn", " "),
                new Claim("email", ""),
                new Claim(ClaimTypes.Email, "last@hospital.example.org")));

            Assert.Equal("last@hospital.example.org", provider.CurrentEmail);
        }

        [Fact]
        public void OnAnUnauthenticatedRequest_NamesNobodyEvenWhenTheClaimIsPresent()
        {
            //an invalid bearer leaves a principal with no claims, so this state does not arise from
            //a request today - the test constructs it deliberately. It is here to pin the rule that
            //User names an established identity and nothing else, so the authentication check is not
            //read as redundant and simplified away once an anonymous endpoint calls a Link service.
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("upn", "attacker@example.org")]))
            };

            Assert.Null(AProvider(httpContext).CurrentEmail);
        }

        [Fact]
        public void OnARequestWithNoPrincipalAtAll_NamesNobody()
        {
            Assert.Null(AProvider(new DefaultHttpContext()).CurrentEmail);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ABlankClaim_NamesNobody(string blank)
        {
            Assert.Null(AProvider(ARequestFrom(new Claim("upn", blank))).CurrentEmail);
        }

        [Fact]
        public void SurroundingWhitespace_IsTrimmed()
        {
            var provider = AProvider(ARequestFrom(new Claim("upn", "  nurse@hospital.example.org  ")));

            Assert.Equal("nurse@hospital.example.org", provider.CurrentEmail);
        }

        [Fact]
        public void TheEmailIsReadOnEveryAccess_SoOneInstanceServesSuccessiveRequests()
        {
            //the provider is a singleton: a value cached on first read would attribute every later
            //call to whoever happened to make the first one.
            var accessor = new HttpContextAccessor
            {
                HttpContext = ARequestFrom(new Claim("upn", "first@hospital.example.org"))
            };
            var provider = new NhsnActorEmailProvider(accessor, Options.Create(new NhsnJwtSettings()));

            Assert.Equal("first@hospital.example.org", provider.CurrentEmail);

            accessor.HttpContext = ARequestFrom(new Claim("upn", "second@hospital.example.org"));

            Assert.Equal("second@hospital.example.org", provider.CurrentEmail);
        }

        [Fact]
        public void WhereTheUserContextFallsBackToAnIdentifier_TheProviderStillNamesNobody()
        {
            //why these are two types rather than one. INhsnUserContext.Email ends at ExternalUserId,
            //which resolves the userId claim first - so on a token carrying userId but no upn it
            //returns an identifier. That is right for describing the signed-in user and wrong for an
            //audit event's User, which a reader takes to be an address. Both now select the email
            //claim through the same helper; this is the difference that must survive that sharing.
            var httpContext = ARequestFrom(new Claim("userId", "NHSN-849217"));
            var settings = new NhsnJwtSettings();

            var userContext = new NhsnUserContext(
                new HttpContextAccessor { HttpContext = httpContext }, Options.Create(settings));

            Assert.Equal("NHSN-849217", userContext.Email);
            Assert.Null(AProvider(httpContext, settings).CurrentEmail);
        }

        [Fact]
        public void TheUserContextSharesTheWidenedFallback_SoAnEmailClaimIsNotReadAsAnIdentifier()
        {
            //the other half of sharing one helper: a gateway that names the email "email" rather
            //than the configured "upn" used to fall through to ExternalUserId here, so the UI and
            //the NhsnUser table recorded an identifier where an address belongs.
            var httpContext = ARequestFrom(
                new Claim("userId", "NHSN-849217"),
                new Claim("email", "nurse@hospital.example.org"));
            var settings = new NhsnJwtSettings();

            var userContext = new NhsnUserContext(
                new HttpContextAccessor { HttpContext = httpContext }, Options.Create(settings));

            Assert.Equal("nurse@hospital.example.org", userContext.Email);
            Assert.Equal("nurse@hospital.example.org", AProvider(httpContext, settings).CurrentEmail);
        }
    }
}
