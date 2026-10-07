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

    [Fact]
    public async Task UploadPackageAsync_SendsOctetStreamToThePackageRoute()
    {
        using var http = new FakeHttpBoundary(string.Empty, 204);
        using var client = CreateClient(http.BaseUrl);

        await client.UploadPackageAsync("nhsn.ig", [1, 2, 3]);
        var request = http.SingleRequest();

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/validation/artifact/PACKAGE/nhsn.ig", request.Path);
        Assert.Equal("application/octet-stream", request.ContentType);
    }

    [Fact]
    public async Task Dependencies_Export_AndRules_UseTheValidationRoutes()
    {
        using (var http = new FakeHttpBoundary("[]"))
        {
            using var client = CreateClient(http.BaseUrl);
            await client.GetAllDependenciesAsync();
            Assert.Equal("/api/validation/artifact/tx-dependencies", http.SingleRequest().Path);
        }

        using (var http = new FakeHttpBoundary("[]"))
        {
            using var client = CreateClient(http.BaseUrl);
            await client.GetPackageDependenciesAsync("nhsn.ig");
            Assert.Equal("/api/validation/artifact/PACKAGE/nhsn.ig/tx-dependencies", http.SingleRequest().Path);
        }

        using (var http = new FakeHttpBoundary("[]"))
        {
            using var client = CreateClient(http.BaseUrl);
            await client.ExportCategoriesAsync();
            var request = http.SingleRequest();
            Assert.Equal("GET", request.Method);
            Assert.Equal("/api/validation/category/$bulk-export", request.Path);
        }

        using (var http = new FakeHttpBoundary(string.Empty, 204))
        {
            using var client = CreateClient(http.BaseUrl);
            await client.ImportCategoriesAsync("[{\"id\":\"lab.rule\"}]");
            var request = http.SingleRequest();
            Assert.Equal("POST", request.Method);
            Assert.Equal("/api/validation/category/$bulk-import", request.Path);
            Assert.Contains("lab.rule", request.Body);
        }

        using (var http = new FakeHttpBoundary(string.Empty, 204))
        {
            using var client = CreateClient(http.BaseUrl);
            await client.SaveCategoryRuleAsync("lab.rule", "{\"field\":\"CODE\",\"regex\":\"^a\",\"inverted\":false}");
            var request = http.SingleRequest();
            Assert.Equal("PUT", request.Method);
            Assert.Equal("/api/validation/category/lab.rule/rule", request.Path);
            Assert.Contains("\"field\":\"CODE\"", request.Body);
        }

        using (var http = new FakeHttpBoundary(string.Empty, 204))
        {
            using var client = CreateClient(http.BaseUrl);
            await client.DeleteCategoryRuleAsync(12);
            var request = http.SingleRequest();
            Assert.Equal("DELETE", request.Method);
            Assert.Equal("/api/validation/category/12", request.Path);
        }
    }

    private static ValidationServiceClient CreateClient(string baseUrl) => new(
        Options.Create(new ServiceRegistry { ValidationServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);
}
