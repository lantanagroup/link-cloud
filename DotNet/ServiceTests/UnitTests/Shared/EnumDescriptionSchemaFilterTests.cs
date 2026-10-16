using System.Xml.Linq;
using LantanaGroup.Link.Shared.Application.Swagger;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace UnitTests.Shared;

/// <summary>
/// Covers how enum members reach the OpenAPI document. Swashbuckle emits an integer enum as a bare list of
/// numbers, so without this filter a client sees a value with nothing saying what it means.
/// </summary>
[Trait("Category", "UnitTests")]
public class EnumDescriptionSchemaFilterTests
{
    public enum SampleStatus
    {
        NotEvaluated = 0,
        NothingToEvaluate = 1
    }

    #region Schema shaping

    [Fact]
    public void EnumNamesArePublishedForClientGeneration()
    {
        var schema = Apply(typeof(SampleStatus));

        // x-enumNames is what NSwag and AutoRest read to generate a named enum instead of a bare int.
        var names = Assert.IsType<OpenApiArray>(schema.Extensions["x-enumNames"]);
        Assert.Equal(
            ["NotEvaluated", "NothingToEvaluate"],
            names.Cast<OpenApiString>().Select(value => value.Value));
    }

    [Fact]
    public void EachMemberIsListedWithItsValueAndName()
    {
        var schema = Apply(typeof(SampleStatus));

        Assert.Contains("`0` **NotEvaluated**", schema.Description);
        Assert.Contains("`1` **NothingToEvaluate**", schema.Description);
    }

    [Fact]
    public void NonEnumSchemasAreLeftAlone()
    {
        var schema = Apply(typeof(string));

        Assert.False(schema.Extensions.ContainsKey("x-enumNames"));
        Assert.Null(schema.Description);
    }

    [Fact]
    public void NullableEnumsAreDocumentedLikeTheirUnderlyingType()
    {
        var schema = Apply(typeof(SampleStatus?));

        // A nullable property reaches the filter as Nullable<T>; unwrapping it is what keeps the members
        // documented on the schema the property actually references.
        Assert.True(schema.Extensions.ContainsKey("x-enumNames"));
    }

    [Fact]
    public void AnExistingTypeDescriptionIsKeptAboveTheMemberList()
    {
        var schema = new OpenApiSchema { Description = "The type's own summary." };

        new EnumDescriptionSchemaFilter().Apply(schema, Context(typeof(SampleStatus)));

        // The filter appends; it must not replace the description IncludeXmlComments already produced.
        Assert.StartsWith("The type's own summary.", schema.Description);
        Assert.Contains("**NotEvaluated**", schema.Description);
    }

    [Fact]
    public void AnEnumWithNoDocumentationFileStillGetsItsNames()
    {
        // The test assembly ships no XML documentation, which is exactly the missing-file path: names must
        // still be published, and the filter must not throw and take the whole spec down with it.
        var schema = Apply(typeof(SampleStatus));

        Assert.Equal(2, Assert.IsType<OpenApiArray>(schema.Extensions["x-enumNames"]).Count);
    }

    #endregion

    #region Documentation parsing

    [Fact]
    public void CrossReferencesResolveToTheNameTheyPointAt()
    {
        var summaries = EnumDescriptionSchemaFilter.ReadFieldSummaries(Documentation());

        // XElement.Value concatenates text nodes only, so a <see cref="..."/> is dropped and the sentence
        // reads "Distinct from , which means...". Cross-references are precisely what enum documentation
        // uses to contrast one value against another, so losing them removes the part worth reading.
        Assert.Equal(
            "The mapping is configured but nothing reached it. Distinct from NotEvaluated, which means no answer has arrived.",
            summaries["F:Sample.Status.NothingToEvaluate"]);
    }

    [Fact]
    public void HardWrappedCommentsAreCollapsedOntoOneLine()
    {
        var summary = EnumDescriptionSchemaFilter.ReadFieldSummaries(Documentation())["F:Sample.Status.NothingToEvaluate"];

        // A documentation file carries the source file's wrapping and indentation. Left alone those land
        // in the rendered description as line breaks and runs of spaces.
        Assert.DoesNotContain("\n", summary);
        Assert.DoesNotContain("  ", summary);
    }

    [Fact]
    public void MembersWithoutASummaryAreSkipped()
    {
        var summaries = EnumDescriptionSchemaFilter.ReadFieldSummaries(Documentation());

        // An undocumented member contributes nothing rather than an empty trailing dash on its line.
        Assert.False(summaries.ContainsKey("F:Sample.Status.Undocumented"));
        Assert.True(summaries.ContainsKey("F:Sample.Status.NotEvaluated"));
    }

    [Fact]
    public void NonFieldMembersAreIgnored()
    {
        var summaries = EnumDescriptionSchemaFilter.ReadFieldSummaries(Documentation());

        // Only F: entries are enum members. Types and methods are already handled by IncludeXmlComments,
        // and pulling them in here would collide with real member ids.
        Assert.DoesNotContain(summaries.Keys, key => key.StartsWith("T:") || key.StartsWith("M:"));
    }

    /// <summary>
    /// A documentation file shaped as the compiler emits one, including the wrapping and the
    /// cross-reference that a naive read drops.
    /// </summary>
    private static XDocument Documentation() => XDocument.Parse("""
        <doc>
          <members>
            <member name="T:Sample.Status">
              <summary>The type itself, which this filter must not treat as a member.</summary>
            </member>
            <member name="F:Sample.Status.NotEvaluated">
              <summary>Nothing has been recorded yet.</summary>
            </member>
            <member name="F:Sample.Status.NothingToEvaluate">
              <summary>
              The mapping is configured but nothing reached it. Distinct from
              <see cref="F:Sample.Status.NotEvaluated"/>, which means no answer has arrived.
              </summary>
            </member>
            <member name="F:Sample.Status.Undocumented">
              <remarks>No summary on this one.</remarks>
            </member>
          </members>
        </doc>
        """);

    #endregion

    private static OpenApiSchema Apply(Type type)
    {
        var schema = new OpenApiSchema();
        new EnumDescriptionSchemaFilter().Apply(schema, Context(type));
        return schema;
    }

    private static SchemaFilterContext Context(Type type) => new(type, null!, null!);
}
