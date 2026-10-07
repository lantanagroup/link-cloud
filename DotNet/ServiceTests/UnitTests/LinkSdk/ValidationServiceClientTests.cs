using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Validation;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class ValidationServiceClientTests
{
    [Fact]
    public async Task UpdateCategoryAsync_SendsCamelCaseJson()
    {
        using var http = new FakeHttpBoundary(string.Empty, 204);
        using var client = CreateClient(http.BaseUrl);

        await client.UpdateCategoryAsync(new ValidationCategoryApiModel
        {
            Id = "lab.rule",
            Title = "Lab",
            Severity = "WARNING",
            Acceptable = true,
            Submit = false,
            Review = true,
            Guidance = "Check the code."
        });
        var request = http.SingleRequest();

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/validation/category/lab.rule", request.Path);
        Assert.Contains("\"id\":\"lab.rule\"", request.Body);
        Assert.Contains("\"submit\":false", request.Body);
        Assert.DoesNotContain("\"Submit\"", request.Body);
        Assert.DoesNotContain("\"Id\"", request.Body);
    }

    private static ValidationServiceClient CreateClient(string baseUrl) => new(
        Options.Create(new ServiceRegistry { ValidationServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);
}
