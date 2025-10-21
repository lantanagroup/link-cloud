using FluentAssertions;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace UnitTests.AdminBFF;

[Trait("Category", "UnitTests")]
public class SwaggerOAuthExtensionTests
{
    private static SwaggerGenOptions CreateOptions()
    {
        var options = new SwaggerGenOptions();
        return options;
    }

    [Fact]
    public void AddOAuthSecurityIfConfigured_adds_definition_and_requirement_when_enabled_with_both_endpoints()
    {
        var options = CreateOptions();

        options.AddOAuthSecurityIfConfigured(
            oauthEnabled: true,
            authorizationEndpoint: "https://auth.example.com/authorize",
            tokenEndpoint: "https://auth.example.com/token");

        options.SwaggerGeneratorOptions.SecuritySchemes
            .Should().ContainKey("OAuth");

        var scheme = options.SwaggerGeneratorOptions.SecuritySchemes["OAuth"];
        scheme.Type.Should().Be(SecuritySchemeType.OAuth2);
        scheme.Flows.AuthorizationCode.AuthorizationUrl
            .Should().Be(new Uri("https://auth.example.com/authorize"));
        scheme.Flows.AuthorizationCode.TokenUrl
            .Should().Be(new Uri("https://auth.example.com/token"));
        scheme.Flows.AuthorizationCode.Scopes
            .Should().ContainKeys("openid", "profile", "email");

        options.SwaggerGeneratorOptions.SecurityRequirements
            .Should().ContainSingle()
            .Which.Keys.Should().ContainSingle()
            .Which.Reference.Id.Should().Be("OAuth");
    }

    [Fact]
    public void AddOAuthSecurityIfConfigured_does_nothing_when_disabled()
    {
        var options = CreateOptions();

        options.AddOAuthSecurityIfConfigured(
            oauthEnabled: false,
            authorizationEndpoint: "https://auth.example.com/authorize",
            tokenEndpoint: "https://auth.example.com/token");

        options.SwaggerGeneratorOptions.SecuritySchemes
            .Should().BeEmpty();
        options.SwaggerGeneratorOptions.SecurityRequirements
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, "https://auth.example.com/token")]
    [InlineData("", "https://auth.example.com/token")]
    [InlineData("   ", "https://auth.example.com/token")]
    [InlineData("https://auth.example.com/authorize", null)]
    [InlineData("https://auth.example.com/authorize", "")]
    [InlineData("https://auth.example.com/authorize", "   ")]
    [InlineData(null, null)]
    public void AddOAuthSecurityIfConfigured_does_nothing_when_endpoint_is_missing(
        string? authorizationEndpoint, string? tokenEndpoint)
    {
        var options = CreateOptions();

        options.AddOAuthSecurityIfConfigured(
            oauthEnabled: true,
            authorizationEndpoint: authorizationEndpoint,
            tokenEndpoint: tokenEndpoint);

        options.SwaggerGeneratorOptions.SecuritySchemes
            .Should().BeEmpty();
        options.SwaggerGeneratorOptions.SecurityRequirements
            .Should().BeEmpty();
    }
}
