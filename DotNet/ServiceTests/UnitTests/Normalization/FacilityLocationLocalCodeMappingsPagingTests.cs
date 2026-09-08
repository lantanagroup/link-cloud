using System.Net;
using System.Text.Json;
using LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;
using LantanaGroup.Link.Normalization.Controllers;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class FacilityLocationLocalCodeMappingsPagingTests
{
    private static readonly string[] Routes =
    [
        "",
        "/search",
        "/facilities/facility-1",
        "/facilities/facility-1/locations/location-1",
        "/facilities/facility-1/local-codes/code-1"
    ];

    public static IEnumerable<object[]> InvalidRequests()
    {
        var cases = new[]
        {
            "pageSize=0",
            "pageSize=-1",
            "pageSize=abc",
            "pageSize=1.5",
            "pageSize=",
            "pageSize=101",
            "pageSize=2147483648",
            "pageSize=-2147483649",
            "PAGESIZE=101"
        };

        foreach (var route in Routes)
        {
            foreach (var testCase in cases)
            {
                yield return [route, testCase];
            }

            var prefix = route == "/search" ? "model" : "paging";
            yield return [route, $"{prefix}.pageSize=101"];
            yield return [route, $"{prefix}.pageSize=abc"];
        }
    }

    public static IEnumerable<object[]> ValidRequests()
    {
        foreach (var route in Routes)
        {
            yield return [route, "", 10];
            yield return [route, "pageSize=1", 1];
            yield return [route, "pageSize=25", 25];
            yield return [route, "pageSize=100", 100];
            yield return [route, "pageSize=1&pageSize=2", 1];
            var prefix = route == "/search" ? "model" : "paging";
            yield return [route, $"{prefix}.pageSize=25", 25];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidPageSize_ReturnsValidationProblemWithoutQuerying(string route, string query)
    {
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>(MockBehavior.Strict);
        using var server = CreateServer(queries);
        using var client = server.CreateClient();

        using var response = await client.GetAsync($"/api/normalization/hsloc-mappings{route}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        var errors = body.RootElement.GetProperty("errors").EnumerateObject().ToList();
        var pageSizeError = Assert.Single(errors, error => error.Name.EndsWith("PageSize", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(pageSizeError.Value[0].GetString()));
        queries.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(ValidRequests))]
    public async Task ValidPageSize_PassesEffectiveSizeToQuery(string route, string query, int expectedPageSize)
    {
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>(MockBehavior.Strict);
        queries.Setup(service => service.Search(It.IsAny<FacilityLocationLocalCodeMappingSearchModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityLocationLocalCodeMappingSearchModel model, CancellationToken cancellationToken) =>
                new PagedConfigModel<FacilityLocationLocalCodeMappingModel>
                {
                    Records = [],
                    Metadata = new PaginationMetadata(model.PageSize!.Value, model.PageNumber ?? 1, 0)
                });
        using var server = CreateServer(queries);
        using var client = server.CreateClient();

        using var response = await client.GetAsync($"/api/normalization/hsloc-mappings{route}?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        queries.Verify(service => service.Search(It.Is<FacilityLocationLocalCodeMappingSearchModel>(model =>
            model.PageSize == expectedPageSize), It.IsAny<CancellationToken>()), Times.Once);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedPageSize, body.RootElement.GetProperty("metadata").GetProperty("pageSize").GetInt32());
    }

    private static TestServer CreateServer(Mock<IFacilityLocationLocalCodeMappingQueries> queries) => new(
        new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddAuthorization(options => options.AddPolicy(PolicyNames.IsLinkAdmin,
                    policy => policy.RequireAssertion(context => true)));
                services.AddSingleton(queries.Object);
                services.AddSingleton(Mock.Of<IFacilityLocationLocalCodeMappingManager>());
                services.AddControllers(options =>
                {
                    options.EnableEndpointRouting = false;
                    options.Filters.Add(new AllowAnonymousFilter());
                }).AddApplicationPart(typeof(FacilityLocationLocalCodeMappingsController).Assembly);
            })
            .Configure(app => app.UseMvc()));
}