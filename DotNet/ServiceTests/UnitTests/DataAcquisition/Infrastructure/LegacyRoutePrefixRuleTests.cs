using FluentAssertions;
using LantanaGroup.Link.DataAcquisition.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;

namespace ServiceTests.UnitTests.DataAcquisition.Infrastructure;

/// <summary>
/// Covers which paths the deprecated /api/data alias rewrites, and that everything else is left alone.
/// </summary>
[Trait("Category", "UnitTests")]
public class LegacyRoutePrefixRuleTests
{
    [Theory]
    [InlineData("/api/data/acquisition-logs", "/api/data-acquisition/acquisition-logs")]
    [InlineData("/api/data/fac-1/QueryPlan/All", "/api/data-acquisition/fac-1/QueryPlan/All")]
    [InlineData("/api/data/info", "/api/data-acquisition/info")]
    [InlineData("/api/data/antiforgery-token", "/api/data-acquisition/antiforgery-token")]
    [InlineData("/api/data", "/api/data-acquisition")]
    [InlineData("/api/data/", "/api/data-acquisition/")]
    public void ApplyRule_LegacyPrefix_RewritesToCanonicalPrefix(string path, string expected)
    {
        var context = Apply(path);

        context.HttpContext.Request.Path.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("/API/DATA/acquisition-logs")]
    [InlineData("/Api/Data/acquisition-logs")]
    public void ApplyRule_LegacyPrefixInAnyCase_IsRewritten(string path)
    {
        // Routing matches case-insensitively, so a case-sensitive alias would 404 requests the old routes served.
        var context = Apply(path);

        context.HttpContext.Request.Path.Value.Should().Be("/api/data-acquisition/acquisition-logs");
    }

    [Theory]
    [InlineData("/api/data-acquisition/acquisition-logs")]
    [InlineData("/api/data-acquisition")]
    [InlineData("/api/database")]
    [InlineData("/api/dataacq/info")]
    [InlineData("/api/data-worker/info")]
    [InlineData("/health")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/other/api/data/acquisition-logs")]
    [InlineData("/")]
    public void ApplyRule_NotLegacyPrefix_LeavesPathUnchanged(string path)
    {
        var context = Apply(path);

        context.HttpContext.Request.Path.Value.Should().Be(path);
        context.HttpContext.Items.Should().NotContainKey(LegacyRoutePrefixRule.LegacyRequestItemKey);
    }

    [Fact]
    public void ApplyRule_LegacyPrefix_MarksRequestAsLegacy()
    {
        var context = Apply("/api/data/acquisition-logs");

        context.HttpContext.Items.Should().ContainKey(LegacyRoutePrefixRule.LegacyRequestItemKey);
    }

    [Fact]
    public void ApplyRule_LegacyPrefixWithQueryString_PreservesQueryString()
    {
        var context = Apply("/api/data/acquisition-logs", "?pageSize=10&pageNumber=2");

        context.HttpContext.Request.Path.Value.Should().Be("/api/data-acquisition/acquisition-logs");
        context.HttpContext.Request.QueryString.Value.Should().Be("?pageSize=10&pageNumber=2");
    }

    [Fact]
    public void ApplyRule_LegacyPrefix_ContinuesToRouting()
    {
        // Ending or short-circuiting the rules would stop the request before it reaches a controller.
        var context = Apply("/api/data/acquisition-logs");

        context.Result.Should().Be(RuleResult.ContinueRules);
    }

    private static RewriteContext Apply(string path, string query = "")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString(query);

        var context = new RewriteContext
        {
            HttpContext = httpContext
        };

        new LegacyRoutePrefixRule().ApplyRule(context);

        return context;
    }
}
