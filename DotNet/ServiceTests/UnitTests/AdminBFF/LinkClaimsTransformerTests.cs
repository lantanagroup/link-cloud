using FluentAssertions;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AdminBFF;

[Trait("Category", "UnitTests")]
public class LinkClaimsTransformerTests
{
    private static LinkClaimsTransformer CreateTransformer(Mock<IGetLinkAccount>? getLinkAccount = null)
    {
        return new LinkClaimsTransformer(
            Mock.Of<ILogger<LinkClaimsTransformer>>(),
            getLinkAccount?.Object ?? Mock.Of<IGetLinkAccount>(),
            Mock.Of<ICacheService>(),
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(new DataProtectionSettings()));
    }

    [Fact]
    public async Task TransformAsync_returns_principal_unchanged_for_system_account()
    {
        var getLinkAccount = new Mock<IGetLinkAccount>(MockBehavior.Strict);
        var transformer = CreateTransformer(getLinkAccount);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(LinkAuthorizationConstants.LinkSystemClaims.Subject, LinkAuthorizationConstants.LinkUserClaims.LinkSystemAccount),
            new Claim(LinkAuthorizationConstants.LinkSystemClaims.Email, "system@test.com"),
            new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, "IsLinkAdmin")
        }, "Bearer");

        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);

        result.Should().BeSameAs(principal);
        result.Identity.Should().BeSameAs(identity);
        identity.Claims.Should().HaveCount(3, "no claims should be added or removed");
        getLinkAccount.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TransformAsync_does_not_bypass_for_non_system_subject()
    {
        var getLinkAccount = new Mock<IGetLinkAccount>();
        getLinkAccount
            .Setup(x => x.ExecuteAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security.Account?)null);

        var transformer = CreateTransformer(getLinkAccount);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(LinkAuthorizationConstants.LinkSystemClaims.Subject, "regular-user-id"),
            new Claim(LinkAuthorizationConstants.LinkSystemClaims.Email, "user@test.com")
        }, "Bearer");

        var principal = new ClaimsPrincipal(identity);

        var result = await transformer.TransformAsync(principal);

        // Account not found → returns empty principal (not the original)
        result.Should().NotBeSameAs(principal);
        result.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task TransformAsync_returns_principal_unchanged_when_no_identity()
    {
        var getLinkAccount = new Mock<IGetLinkAccount>(MockBehavior.Strict);
        var transformer = CreateTransformer(getLinkAccount);

        var principal = new ClaimsPrincipal();

        var result = await transformer.TransformAsync(principal);

        result.Should().BeSameAs(principal);
        getLinkAccount.VerifyNoOtherCalls();
    }
}
