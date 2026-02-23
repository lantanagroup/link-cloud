using LantanaGroup.Link.Shared.Application.Services.Security;

namespace UnitTests.Shared.StringSecurityExtensions;

[Trait("Category", "UnitTests")]
public class StringSecurityExtensionsTest
{
    [Theory]
    [InlineData("Hello World", "Hello World")]
    [InlineData("Test123", "Test123")]
    [InlineData("abc!@#$%^&*()xyz", "abc!@#$%^&*()xyz")]
    [InlineData("Valid-String_With.Special~Chars", "Valid-String_With.Special~Chars")]
    public void SanitizeUntrustedString_WithPrintableAscii_ReturnsUnchanged(string input, string expected)
    {
        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Hello\nWorld", "Hello World")]
    [InlineData("Test\rString", "Test String")]
    [InlineData("Line1\r\nLine2", "Line1  Line2")]
    [InlineData("Tab\tSeparated", "Tab Separated")]
    public void SanitizeUntrustedString_WithControlCharacters_ReplacesWithSpace(string input, string expected)
    {
        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithNullCharacter_ReplacesWithSpace()
    {
        // Arrange
        var input = "Hello\x00World";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithLowControlCharacters_ReplacesWithSpace()
    {
        // Arrange
        var input = "\x01\x02\x03";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal("   ", result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithEscapeCharacter_ReplacesWithSpace()
    {
        // Arrange
        var input = "Test\x1BString";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal("Test String", result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithDeleteCharacter_ReplacesWithSpace()
    {
        // Arrange - DEL character (ASCII 127)
        var input = "Test" + (char)127 + "Delete";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal("Test Delete", result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithExtendedAsciiCharacter_PreservesCharacter()
    {
        // Arrange - Extended ASCII (character 128)
        var input = "Test" + (char)128 + "String";
        var expected = "Test" + (char)128 + "String";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithUnicodeCharacters_PreservesCharacters()
    {
        // Arrange - "Café" using Unicode character é (U+00E9)
        var input = "Caf" + (char)0x00E9;
        var expected = "Café";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithSpanishCharacters_PreservesCharacters()
    {
        // Arrange - "Ñoño" using Ñ (U+00D1) and ñ (U+00F1)
        var input = "" + (char)0x00D1 + "o" + (char)0x00F1 + "o";
        var expected = "Ñoño";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithAllPrintableAsciiRange_ReturnsUnchanged()
    {
        // Arrange - ASCII 32 (space) through 126 (tilde)
        var input = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("Log entry\nFake timestamp: malicious data", "Log entry Fake timestamp: malicious data")]
    [InlineData("User: admin\r\nPassword: leaked", "User: admin  Password: leaked")]
    public void SanitizeUntrustedString_WithLogInjectionAttempts_RemovesInjectionVectors(string input, string expected)
    {
        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithLogInjectionAndNullBytes_RemovesInjectionVectors()
    {
        // Arrange
        var input = "Normal log\x00\x01Hidden data";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal("Normal log  Hidden data", result);
    }

    [Theory]
    [InlineData("~", "~")]
    [InlineData(" ", " ")]
    [InlineData("}", "}")]
    public void SanitizeUntrustedString_WithBoundaryCharacters_HandlesCorrectly(string input, string expected)
    {
        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithLongString_HandlesPerformance()
    {
        // Arrange
        var input = new string('A', 10000) + "\n" + new string('B', 10000);

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(20001, result.Length);
        Assert.Equal(' ', result[10000]);
    }

    [Fact]
    public void SanitizeUntrustedString_PreservesSpaces()
    {
        // Arrange
        var input = "Hello   World";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal("Hello   World", result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithAllControlCharacters_ReplacesAll()
    {
        // Arrange - Test all control characters (0-31)
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i <= 31; i++)
        {
            builder.Append((char)i);
        }
        var input = builder.ToString();

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal(new string(' ', 32), result);
    }

    [Fact]
    public void SanitizeUntrustedString_WithCharacter127_ReplacesWithSpace()
    {
        // Arrange
        var input = $"Test{(char)127}End";

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Equal("Test End", result);
    }

    [Fact]
    public void SanitizeUntrustedString_HandlesNull()
    {
        // Arrange
        string? input = null;

        // Act
        var result = input.SanitizeUntrustedString();

        // Assert
        Assert.Null(result);
    }
}
