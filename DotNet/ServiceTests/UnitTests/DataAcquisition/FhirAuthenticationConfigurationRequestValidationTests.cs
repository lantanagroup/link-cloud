using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using System.ComponentModel.DataAnnotations;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class FhirAuthenticationConfigurationRequestValidationTests
{
    /// <summary>
    /// Runs the same DataAnnotations + IValidatableObject validation that MVC model binding runs,
    /// without the HTTP pipeline.
    /// </summary>
    private static (bool isValid, List<ValidationResult> results) Validate(FhirAuthenticationConfigurationRequest model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return (isValid, results);
    }

    private static FhirAuthenticationConfigurationRequest CreateValidRequest() => new()
    {
        TokenUrl = "https://vendor.test/oauth2/token",
        ClientId = "link-client",
        ClientSecret = "s3cret",
        Scope = "system/*.read"
    };

    private static List<ValidationResult> ErrorsFor(List<ValidationResult> results, string memberName)
    {
        return results
            .Where(r => r.MemberNames.Contains(memberName))
            .ToList();
    }

    // ---- The happy path ----

    [Fact]
    public void Validate_FullyPopulatedRequest_Passes()
    {
        var (isValid, results) = Validate(CreateValidRequest());

        Assert.True(isValid, "Expected a fully-populated request to pass validation. Failures: " +
            string.Join("; ", results.Select(r => r.ErrorMessage)));
        Assert.Empty(results);
    }

    // ---- TokenUrl ----

    [Theory]
    [InlineData("https://vendor.test/oauth2/token")]
    [InlineData("https://vendor.test:8443/oauth2/token")]
    [InlineData("https://vendor.test/token?tenant=abc")]
    [InlineData("  https://vendor.test/oauth2/token  ")]
    public void Validate_AcceptableTokenUrl_Passes(string tokenUrl)
    {
        var request = CreateValidRequest();
        request.TokenUrl = tokenUrl;

        var (isValid, results) = Validate(request);

        Assert.True(isValid, "Failures: " + string.Join("; ", results.Select(r => r.ErrorMessage)));
    }

    /// <summary>
    /// http is deliberately allowed, matching ConnectionValidationRequestValidator.ValidateFhirServerUrl.
    /// This test exists so that flipping to https-only is a visible decision rather than a silent one.
    /// </summary>
    [Fact]
    public void Validate_HttpTokenUrl_Passes()
    {
        var request = CreateValidRequest();
        request.TokenUrl = "http://localhost:8080/oauth2/token";

        var (isValid, _) = Validate(request);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankTokenUrl_ReportsRequired(string tokenUrl)
    {
        var request = CreateValidRequest();
        request.TokenUrl = tokenUrl;

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        var error = Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.TokenUrl)));
        Assert.Equal("TokenUrl is required.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("oauth2/token")]
    [InlineData("/oauth2/token")]
    [InlineData("not a url")]
    public void Validate_RelativeTokenUrl_ReportsNotAbsolute(string tokenUrl)
    {
        var request = CreateValidRequest();
        request.TokenUrl = tokenUrl;

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        var error = Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.TokenUrl)));
        Assert.Equal("TokenUrl must be a valid absolute URL.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("ftp://vendor.test/token")]
    [InlineData("file:///c:/token")]
    public void Validate_NonHttpTokenUrlScheme_ReportsScheme(string tokenUrl)
    {
        var request = CreateValidRequest();
        request.TokenUrl = tokenUrl;

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        var error = Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.TokenUrl)));
        Assert.Equal("TokenUrl must use the http or https scheme.", error.ErrorMessage);
    }

    /// <summary>
    /// RFC 6749 section 3.2 allows a query string on the token endpoint but forbids a fragment.
    /// </summary>
    [Fact]
    public void Validate_TokenUrlWithFragment_ReportsFragment()
    {
        var request = CreateValidRequest();
        request.TokenUrl = "https://vendor.test/oauth2/token#section";

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        var error = Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.TokenUrl)));
        Assert.Equal("TokenUrl must not include a fragment.", error.ErrorMessage);
    }

    // ---- ClientId and Scope ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankClientId_ReportsRequired(string clientId)
    {
        var request = CreateValidRequest();
        request.ClientId = clientId;

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        var error = Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.ClientId)));
        Assert.Equal("ClientId is required.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankScope_ReportsRequired(string scope)
    {
        var request = CreateValidRequest();
        request.Scope = scope;

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        var error = Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.Scope)));
        Assert.Equal("Scope is required.", error.ErrorMessage);
    }

    // ---- ClientSecret ----

    /// <summary>
    /// An absent secret means "keep the stored one". Whether one is actually stored is the service's
    /// call, so the model must not reject a null here.
    /// </summary>
    [Fact]
    public void Validate_NullClientSecret_Passes()
    {
        var request = CreateValidRequest();
        request.ClientSecret = null;

        var (isValid, results) = Validate(request);

        Assert.True(isValid, "Failures: " + string.Join("; ", results.Select(r => r.ErrorMessage)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankClientSecret_ReportsNotBlank(string clientSecret)
    {
        var request = CreateValidRequest();
        request.ClientSecret = clientSecret;

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        var error = Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.ClientSecret)));
        Assert.Equal("ClientSecret must not be blank when it is supplied.", error.ErrorMessage);
    }

    // ---- Reporting every failure at once ----

    /// <summary>
    /// The endpoint returns a 400 listing every bad field, so one round trip is enough to fix the request.
    /// </summary>
    [Fact]
    public void Validate_EmptyRequest_ReportsEveryRequiredField()
    {
        var request = new FhirAuthenticationConfigurationRequest();

        var (isValid, results) = Validate(request);

        Assert.False(isValid);
        Assert.Equal(3, results.Count);
        Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.TokenUrl)));
        Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.ClientId)));
        Assert.Single(ErrorsFor(results, nameof(FhirAuthenticationConfigurationRequest.Scope)));
    }
}
