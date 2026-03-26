using LantanaGroup.Link.Shared.Application.Services.Security;

namespace UnitTests.Shared.HtmlInputSanitzer;

[Trait("Category", "UnitTests")]
public class HtmlInputSanitizerTest
{
    [Theory]
    [InlineData("<script>alert('XSS');</script>", "")]
    [InlineData("alert('XSS');</script>", "alertXSS")]
    [InlineData("Hello, World!", "Hello World")]
    [InlineData("123-456_789", "123-456_789")]
    [InlineData("!@#$%^&*()", "amp")]       // Sanitizer class converts & to &amp; then regex removes & and ;
    [InlineData("", "")]
    [InlineData("smoke-test-facility", "smoke-test-facility")]
    [InlineData(null, "")]
    public void TestSanitizeAndRemove(string input, string expected)
    {
        // Act
        var result = input.SanitizeAndRemove();

        // Assert
        Assert.Equal(expected, result);
    }
}