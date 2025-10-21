using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LantanaGroup.Link.Shared.Application.Swagger;

/// <summary>
/// Documents enum members in the generated OpenAPI schema, by name and by the XML comment on each member.
/// </summary>
/// <remarks>
/// <para>
/// Swashbuckle emits an integer enum as a bare list of numbers — <c>enum: [0,1,2,3,4,5,6,7,8]</c> — with no
/// indication of what any of them mean. <c>IncludeXmlComments</c> does not help: it documents types,
/// methods and properties, but never enum members. A client developer reading the spec sees an 8 and has to
/// go and find the C# source to learn it means "excluded".
/// </para>
/// <para>
/// This appends the names and their documentation to the schema description, and also publishes
/// <c>x-enumNames</c>, the extension NSwag and AutoRest read when generating client enums. The wire format
/// is deliberately unchanged: switching to string serialization would document the values at the cost of
/// breaking every existing consumer of the numeric ones.
/// </para>
/// <para>
/// An enum whose assembly ships no XML documentation file still gets its names, just without the prose.
/// </para>
/// </remarks>
public sealed class EnumDescriptionSchemaFilter : ISchemaFilter
{
    /// <summary>
    /// Member summaries per assembly, keyed by the XML documentation member id (<c>F:Namespace.Type.Name</c>).
    /// Cached because the filter runs once per schema and the file would otherwise be re-read each time.
    /// </summary>
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Summaries = new();

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Type) ?? context.Type;

        if (!type.IsEnum)
        {
            return;
        }

        var documentation = SummariesFor(type.Assembly);
        var names = new OpenApiArray();
        var lines = new StringBuilder();

        foreach (var name in Enum.GetNames(type))
        {
            names.Add(new OpenApiString(name));

            var value = Convert.ToInt64(Enum.Parse(type, name));
            lines.Append("\n- `").Append(value).Append("` **").Append(name).Append('*').Append('*');

            if (documentation.TryGetValue($"F:{type.FullName}.{name}", out var summary))
            {
                lines.Append(" — ").Append(summary);
            }
        }

        // Read by NSwag and AutoRest to generate a named client enum rather than a bare integer.
        schema.Extensions["x-enumNames"] = names;

        schema.Description = string.IsNullOrWhiteSpace(schema.Description)
            ? lines.ToString().TrimStart('\n')
            : schema.Description.TrimEnd() + "\n" + lines;
    }

    private static IReadOnlyDictionary<string, string> SummariesFor(Assembly assembly)
    {
        var name = assembly.GetName().Name;

        if (string.IsNullOrEmpty(name))
        {
            return new Dictionary<string, string>();
        }

        return Summaries.GetOrAdd(name, static assemblyName =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".xml");

            // Not every assembly ships documentation, and a service must not fail to serve its spec because
            // of it. Names alone are still a large improvement over bare integers.
            if (!File.Exists(path))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                return ReadFieldSummaries(XDocument.Load(path));
            }
            catch (Exception)
            {
                // A malformed or partially written documentation file is not worth a broken spec.
                return new Dictionary<string, string>();
            }
        });
    }

    /// <summary>
    /// Reads every field summary out of an XML documentation file, keyed by its member id.
    /// </summary>
    /// <remarks>
    /// Separate from the file loading so the parsing and cross-reference handling can be exercised
    /// directly. Resolving them through a real assembly's documentation would make the test depend on the
    /// test project generating a documentation file, which it does not.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> ReadFieldSummaries(XDocument document) =>
        document
            .Descendants("member")
            .Where(member => member.Attribute("name")?.Value.StartsWith("F:") == true)
            .Where(member => member.Element("summary") is not null)
            .GroupBy(member => member.Attribute("name")!.Value)
            .ToDictionary(
                group => group.Key,
                group => Collapse(Flatten(group.First().Element("summary")!)));

    /// <summary>
    /// Renders the content of a documentation element as plain text, resolving cross-references to the name
    /// they point at.
    /// </summary>
    /// <remarks>
    /// <see cref="XElement.Value"/> concatenates text nodes only, so a summary written as
    /// <c>Distinct from &lt;see cref="NotApplicable"/&gt;, which means...</c> silently loses the reference
    /// and reads as "Distinct from , which means...". Cross-references are exactly what an enum member's
    /// documentation uses to contrast one value with another, so dropping them removes the part worth
    /// reading.
    /// </remarks>
    private static string Flatten(XElement element)
    {
        var text = new StringBuilder();

        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText content:
                    text.Append(content.Value);
                    break;

                case XElement child when child.Name == "see" || child.Name == "seealso":
                    text.Append(MemberName(child.Attribute("cref")?.Value ?? child.Attribute("langword")?.Value));
                    break;

                case XElement child when child.Name == "paramref" || child.Name == "typeparamref":
                    text.Append(child.Attribute("name")?.Value);
                    break;

                case XElement child:
                    text.Append(Flatten(child));
                    break;
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Reduces a documentation cref to the name a reader recognises: <c>T:Some.Namespace.Type.Member</c>
    /// becomes <c>Member</c>.
    /// </summary>
    private static string MemberName(string? cref)
    {
        if (string.IsNullOrWhiteSpace(cref))
        {
            return string.Empty;
        }

        var withoutPrefix = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var lastSeparator = withoutPrefix.LastIndexOf('.');

        return lastSeparator >= 0 ? withoutPrefix[(lastSeparator + 1)..] : withoutPrefix;
    }

    /// <summary>
    /// Flattens the hard-wrapped, indented text of an XML comment into one line, so it reads as a sentence
    /// in the rendered description rather than carrying the source file's line breaks.
    /// </summary>
    private static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
