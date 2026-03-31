using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

namespace UnitTests.DataAcquisition.FhirCommandUtilities;

public class FhirCommandUtilsTests
{
    [Theory]
    [InlineData("30", 30)]  // Positive seconds
    [InlineData("0", 60)]    // Zero seconds (immediate retry)
    [InlineData(null, 60)]  // Missing header → default 60s
    public void ParseRetryAfter_HandlesDeltaFormats(string? headerValue, int expectedSeconds)
    {
        // Arrange
        var response = new HttpResponseMessage();
        if (headerValue != null)
        {
            response.Headers.Add("Retry-After", headerValue);
        }
        var headers = response.Headers;

        // Act
        var delay = FhirCommandUtils.ParseRetryAfter(headers);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void ParseRetryAfter_HandlesFutureDateFormat()
    {
        // Arrange
        var futureDate = DateTimeOffset.UtcNow.AddMinutes(5);  // Future: ~5min delay
        var headerValue = futureDate.ToString("R");  // RFC1123 format (e.g., "Fri, 16 Jan 2026 21:47:00 GMT")
        var response = new HttpResponseMessage();
        response.Headers.Add("Retry-After", headerValue);
        var headers = response.Headers;

        // Act
        var delay = FhirCommandUtils.ParseRetryAfter(headers);

        // Assert: Positive delay ~5min (allow slight variance for execution time)
        Assert.True(delay >= TimeSpan.FromMinutes(4.9) && delay <= TimeSpan.FromMinutes(5.1));
    }

    [Fact]
    public void ParseRetryAfter_CustomDefault_Overrides()
    {
        // Arrange
        var response = new HttpResponseMessage();  // No Retry-After

        // Act
        var delay = FhirCommandUtils.ParseRetryAfter(response.Headers, TimeSpan.FromSeconds(120));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(120), delay);  // Uses custom default
    }
}