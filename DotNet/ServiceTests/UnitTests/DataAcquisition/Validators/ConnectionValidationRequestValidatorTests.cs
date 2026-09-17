using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;

namespace UnitTests.DataAcquisition.Validators;

[Trait("Category", "UnitTests")]
public class ConnectionValidationRequestValidatorTests
{
    [Theory]
    [InlineData("http://fhir.test/r4")]
    [InlineData("https://fhir.test/r4")]
    [InlineData("https://fhir.test/r4/")]
    [InlineData("  https://fhir.test/r4  ")]
    [InlineData("https://fhir.test")]
    [InlineData("https://fhir.test:8443/r4")]
    public void ValidateFhirServerUrl_BaseUrl_IsValid(string fhirServerUrl)
    {
        var isValid = ConnectionValidationRequestValidator.ValidateFhirServerUrl(fhirServerUrl, out var errorMessage);

        Assert.True(isValid);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Theory]
    [InlineData("http://fhir.test/r4?foo=bar")]
    [InlineData("https://fhir.test/r4/?foo=bar")]
    [InlineData("https://fhir.test?")]
    [InlineData("http://fhir.test/r4#fragment")]
    [InlineData("https://fhir.test/r4/#")]
    [InlineData("https://fhir.test/r4?foo=bar#fragment")]
    public void ValidateFhirServerUrl_UrlWithQueryOrFragment_IsRejected(string fhirServerUrl)
    {
        var isValid = ConnectionValidationRequestValidator.ValidateFhirServerUrl(fhirServerUrl, out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("base URL", errorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateFhirServerUrl_MissingUrl_IsRejected(string? fhirServerUrl)
    {
        var isValid = ConnectionValidationRequestValidator.ValidateFhirServerUrl(fhirServerUrl, out var errorMessage);

        Assert.False(isValid);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    public void ValidateFhirServerUrl_NotAnAbsoluteUrl_IsRejected(string fhirServerUrl)
    {
        var isValid = ConnectionValidationRequestValidator.ValidateFhirServerUrl(fhirServerUrl, out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("absolute", errorMessage);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://fhir.test/r4")]
    public void ValidateFhirServerUrl_UnsupportedScheme_IsRejected(string fhirServerUrl)
    {
        var isValid = ConnectionValidationRequestValidator.ValidateFhirServerUrl(fhirServerUrl, out var errorMessage);

        Assert.False(isValid);
        Assert.Contains("scheme", errorMessage);
    }
}
