using System.Reflection;
using LantanaGroup.Link.Terminology.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace UnitTests.Terminology;

/// <summary>
/// Covers <see cref="CodeSearchQuery"/> directly: the sanitizing its setters apply, the parameter names
/// it binds under, and the combination rules <see cref="CodeSearchQuery.Validate"/> enforces.
/// </summary>
/// <remarks>
/// The HTTP tests reach this class only through model binding, which cannot produce several of the states
/// it has to handle - MVC converts an empty or whitespace query value to null before the action runs, and
/// nothing over HTTP can construct the query with a null assigned explicitly. Both are exercised here.
/// </remarks>
public class CodeSearchQueryTests
{
    private const string Hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";

    // ------------------------------------------------------------- sanitizing

    /// <summary>
    /// Null has to survive as null. Sanitize returns an empty string for null input, so cleaning without
    /// a null check would make every omitted parameter arrive as "" - which Validate then rejects as
    /// blank, failing a request that named nothing wrong.
    /// </summary>
    [Fact]
    public void OmittedValues_StayNullRatherThanBecomingEmpty()
    {
        var query = new CodeSearchQuery
        {
            Search = null,
            CodeSystem = null,
            ValueSet = null,
            Version = null
        };

        Assert.Null(query.Search);
        Assert.Null(query.CodeSystem);
        Assert.Null(query.ValueSet);
        Assert.Null(query.Version);

        // An empty query is still refused for naming none of the three, but never for being "blank" -
        // that distinction is the whole point of preserving null.
        var errors = new CodeSearchQuery { Search = "burn" }.Validate();

        Assert.True(errors.IsValid);
    }

    /// <summary>
    /// A canonical URI has to survive intact. SanitizeAndRemove would strip its ":" and "/", leaving a URI
    /// that matches no loaded code group.
    /// </summary>
    [Fact]
    public void CanonicalUris_SurviveSanitizingUnaltered()
    {
        var query = new CodeSearchQuery { CodeSystem = Hsloc, Version = "1.0.0-rc1" };

        Assert.Equal(Hsloc, query.CodeSystem);
        Assert.Equal("1.0.0-rc1", query.Version);
    }

    /// <summary>
    /// Sanitizing HTML-encodes what it keeps, so an ampersand becomes "&amp;amp;" unless it is decoded
    /// again - and over 2,000 LOINC displays contain one.
    /// </summary>
    [Fact]
    public void ReservedCharactersInSearchText_SurviveSanitizing()
    {
        var query = new CodeSearchQuery { Search = "Labor & Delivery <5 yrs" };

        Assert.Equal("Labor & Delivery <5 yrs", query.Search);
    }

    /// <summary>
    /// Dangerous markup is removed outright. What is left is an empty string rather than null, so Validate
    /// can tell "supplied but unusable" from "not supplied" and reject the first.
    /// </summary>
    [Fact]
    public void DangerousMarkupSanitizesToEmptyAndIsRejected()
    {
        var query = new CodeSearchQuery { Search = "burn", CodeSystem = "<script>alert(1)</script>" };

        Assert.Equal(string.Empty, query.CodeSystem);

        var errors = query.Validate();

        Assert.False(errors.IsValid);
        Assert.True(errors.ContainsKey(CodeSearchParameters.CodeSystem));
    }

    [Fact]
    public void ScriptTagsAreStripped()
    {
        var query = new CodeSearchQuery { Search = "burn<script>alert(1)</script>" };

        Assert.DoesNotContain("<script", query.Search);
        Assert.Contains("burn", query.Search);
    }

    /// <summary>
    /// The sanitizer allows benign formatting tags, so they reach the matcher intact and simply fail to
    /// match anything - "&lt;b&gt;burn&lt;/b&gt;" is a search for that literal text, not for "burn". Recorded
    /// because it is easy to assume every tag is stripped; only dangerous ones are.
    /// </summary>
    [Fact]
    public void BenignFormattingTagsAreKept_NotStripped()
    {
        var query = new CodeSearchQuery { Search = "<b>burn</b>" };

        Assert.Equal("<b>burn</b>", query.Search);
        Assert.True(query.Validate().IsValid);
    }

    // ---------------------------------------------------------- binding names

    /// <summary>
    /// The bound name is what Swagger publishes and what a generated client sends. Without these
    /// attributes Swashbuckle takes the PascalCase property names, and the spec then disagrees with the
    /// operation description, with the errors object on a 400, and with the ticket.
    /// </summary>
    [Theory]
    [InlineData(nameof(CodeSearchQuery.Search), "search")]
    [InlineData(nameof(CodeSearchQuery.CodeSystem), "codeSystem")]
    [InlineData(nameof(CodeSearchQuery.ValueSet), "valueSet")]
    [InlineData(nameof(CodeSearchQuery.Version), "version")]
    [InlineData(nameof(CodeSearchQuery.ExcludeInactive), "excludeInactive")]
    [InlineData(nameof(CodeSearchQuery.PageNumber), "pageNumber")]
    [InlineData(nameof(CodeSearchQuery.PageSize), "pageSize")]
    public void EveryParameterBindsUnderTheNameCallersUse(string propertyName, string expected)
    {
        var attribute = typeof(CodeSearchQuery)
            .GetProperty(propertyName)!
            .GetCustomAttribute<FromQueryAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(expected, attribute.Name);
    }

    /// <summary>
    /// The four string parameters take their bound name from the same constants the validation errors and
    /// <c>CodeSearchService</c> use, so the three cannot drift apart.
    /// </summary>
    [Fact]
    public void TheStringParametersShareTheirNamesWithTheErrorKeys()
    {
        static string? BoundName(string property) => typeof(CodeSearchQuery)
            .GetProperty(property)!
            .GetCustomAttribute<FromQueryAttribute>()!
            .Name;

        Assert.Equal(CodeSearchParameters.Search, BoundName(nameof(CodeSearchQuery.Search)));
        Assert.Equal(CodeSearchParameters.CodeSystem, BoundName(nameof(CodeSearchQuery.CodeSystem)));
        Assert.Equal(CodeSearchParameters.ValueSet, BoundName(nameof(CodeSearchQuery.ValueSet)));
        Assert.Equal(CodeSearchParameters.Version, BoundName(nameof(CodeSearchQuery.Version)));
    }

    // --------------------------------------------------------------- defaults

    [Fact]
    public void PagingDefaultsToTheFirstPageAtTheServerDefaultSize()
    {
        var query = new CodeSearchQuery();

        Assert.Equal(1, query.PageNumber);
        Assert.Equal(CodeSearchDefaults.DefaultPageSize, query.PageSize);
    }

    /// <summary>
    /// More is less: inactive codes are returned unless the caller opts out.
    /// </summary>
    [Fact]
    public void InactiveCodesAreIncludedByDefault()
    {
        Assert.False(new CodeSearchQuery().ExcludeInactive);
    }

    // ------------------------------------------------------------- validation

    [Fact]
    public void AQueryConstrainedBySearchAloneIsValid()
    {
        Assert.True(new CodeSearchQuery { Search = "burn" }.Validate().IsValid);
    }

    [Fact]
    public void AQueryConstrainedByCodeSystemAloneIsValid()
    {
        Assert.True(new CodeSearchQuery { CodeSystem = Hsloc }.Validate().IsValid);
    }

    [Fact]
    public void NamingNoneOfTheThreeIsRejected()
    {
        var errors = new CodeSearchQuery().Validate();

        Assert.False(errors.IsValid);
        Assert.True(errors.ContainsKey(CodeSearchParameters.Search));
    }

    [Fact]
    public void NamingBothACodeSystemAndAValueSetIsRejectedAgainstBoth()
    {
        var errors = new CodeSearchQuery { CodeSystem = Hsloc, ValueSet = "http://example.org/vs" }.Validate();

        Assert.False(errors.IsValid);
        Assert.True(errors.ContainsKey(CodeSearchParameters.CodeSystem));
        Assert.True(errors.ContainsKey(CodeSearchParameters.ValueSet));
    }

    [Fact]
    public void AVersionWithoutAScopeIsRejected()
    {
        var errors = new CodeSearchQuery { Search = "burn", Version = "1.0.0" }.Validate();

        Assert.False(errors.IsValid);
        Assert.True(errors.ContainsKey(CodeSearchParameters.Version));
    }

    /// <summary>
    /// The length is measured against the cleaned value, so markup cannot be used to pad a search up to
    /// the minimum and force the scan the minimum exists to prevent.
    /// </summary>
    [Fact]
    public void SearchLengthIsMeasuredAfterSanitizing()
    {
        var query = new CodeSearchQuery { Search = "ab<script>alert(1)</script>" };

        Assert.Equal("ab", query.Search);

        var errors = query.Validate();

        Assert.False(errors.IsValid);
        Assert.True(errors.ContainsKey(CodeSearchParameters.Search));
    }

    [Fact]
    public void ASearchOfExactlyTheMinimumLengthIsAccepted()
    {
        var search = new string('a', CodeSearchDefaults.MinSearchLength);

        Assert.True(new CodeSearchQuery { Search = search }.Validate().IsValid);
    }

    /// <summary>
    /// Every problem is reported at once rather than short-circuiting on the first, so a caller who got
    /// two things wrong is told about both instead of fixing one and being refused again.
    /// </summary>
    [Fact]
    public void EveryProblemIsReportedAtOnce()
    {
        var errors = new CodeSearchQuery
        {
            Search = "ab",
            CodeSystem = Hsloc,
            ValueSet = "http://example.org/vs"
        }.Validate();

        Assert.False(errors.IsValid);
        Assert.True(errors.ContainsKey(CodeSearchParameters.Search));
        Assert.True(errors.ContainsKey(CodeSearchParameters.CodeSystem));
        Assert.True(errors.ContainsKey(CodeSearchParameters.ValueSet));
    }
}
