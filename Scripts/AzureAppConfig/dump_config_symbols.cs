// The repo root has Directory.Packages.props, so Central Package Management applies to this
// file too and would reject an inline version. Opt out rather than adding a PackageVersion
// entry that every project in the solution would then carry for the sake of one script.
#:property ManagePackageVersionsCentrally=false
// File-based apps default to AOT-friendly settings, which switch off reflection-based
// System.Text.Json. This dump serialises anonymous types, so turn it back on.
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:package Microsoft.CodeAnalysis.CSharp@4.14.0

// Dump the C# symbol facts that configuration-key extraction depends on.
//
// Run as a file-based app - no project, no solution load:
//
//     dotnet run --file Scripts/AzureAppConfig/dump_config_symbols.cs -- DotNet Scripts/AzureAppConfig/config_symbols.json
//
// Why this exists
// ---------------
// Scripts/AzureAppConfig/extract_config_keys.py finds the *call sites* that read configuration. To turn a
// bound section into the sub-keys it supplies, it needs to know what a settings type actually
// contains. Deriving that from text was wrong in four distinct ways, all of which are free
// here because Roslyn resolves symbols rather than matching characters:
//
//   * `public static string SectionName` and `public const string SectionName` are both
//     section-name declarations. A regex for `const` silently loses KafkaConstants.SectionName
//     and ServiceRegistry.ConfigSectionName - the two most-referenced sections in the codebase.
//   * Six different classes declare a member called `SectionName`. Resolving that name without
//     a symbol table files one section's keys under another section's name.
//   * ExternalBlobStorageSettings inherits ConnectionString and BlobContainerName from
//     BlobStorageSettings. Text scanning sees an empty class.
//   * TelemetrySettings.EnableOtelCollector is a public *field*, not a property.
//     ConfigurationBinder binds properties only, so a value set in a store can never apply.
//     Only a symbol model distinguishes the two reliably.
//
// A full MSBuildWorkspace load of the 25-project solution is not needed. Every type this cares
// about is declared in source, so compiling the source files together is enough for base types,
// member kinds and constant values to resolve against each other. Unresolved framework types
// simply do not expand, which is the correct outcome for them anyway.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var root = args.Length > 0 ? args[0] : "DotNet";
var outputPath = args.Length > 1 ? args[1] : "Scripts/AzureAppConfig/config_symbols.json";

string[] skipDirs = ["bin", "obj", ".vs", "node_modules", "Migrations"];
string[] skipProjects = ["ServiceTests", "Audit.Specification"];

var sources = Directory
    .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    .Where(p =>
    {
        var parts = p.Replace('\\', '/').Split('/');
        return !parts.Any(skipDirs.Contains) && !parts.Any(skipProjects.Contains);
    })
    .ToList();

Console.Error.WriteLine($"Parsing {sources.Count} source files from {root}...");

var trees = sources
    .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p))
    .ToList();

// Reference the core framework so string/List<T>/Dictionary<,> resolve. Link's own types
// resolve against each other because every source file is in the same compilation.
var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator)
    .Where(p => p.EndsWith(".dll"))
    .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
    .ToList();

var compilation = CSharpCompilation.Create(
    "ConfigSymbolDump",
    trees,
    trustedAssemblies,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

// Produce type names in C# spelling, which is what the downstream collection/dictionary
// patterns expect: "string" not "String", "List<string>", "int?" not "Nullable<Int32>".
// UseSpecialTypes is what maps the CLR names back to the language aliases.
var typeFormat = new SymbolDisplayFormat(
    globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

var types = new Dictionary<string, TypeInfo>();
var constants = new Dictionary<string, HashSet<string>>();

void AddConstant(string name, string value)
{
    if (!constants.TryGetValue(name, out var set))
    {
        constants[name] = set = new HashSet<string>();
    }
    set.Add(value);
}

IEnumerable<INamedTypeSymbol> AllTypes(INamespaceOrTypeSymbol symbol)
{
    foreach (var member in symbol.GetMembers())
    {
        if (member is INamespaceOrTypeSymbol child)
        {
            if (child is INamedTypeSymbol named)
            {
                yield return named;
            }
            foreach (var nested in AllTypes(child))
            {
                yield return nested;
            }
        }
    }
}

foreach (var type in AllTypes(compilation.GlobalNamespace))
{
    if (type.Locations.All(l => !l.IsInSource))
    {
        continue; // framework type pulled in by reference
    }

    // Qualified path through containing types, so ConfigurationConstants.AppSettings.CORS
    // can be resolved at any level of qualification.
    var path = new List<string>();
    for (var current = type; current is not null; current = current.ContainingType)
    {
        path.Insert(0, current.Name);
    }

    var info = types.TryGetValue(type.Name, out var existing)
        ? existing
        : types[type.Name] = new TypeInfo();

    foreach (var baseType in Bases(type))
    {
        if (!info.Bases.Contains(baseType))
        {
            info.Bases.Add(baseType);
        }
    }

    foreach (var member in type.GetMembers())
    {
        if (member.DeclaredAccessibility != Accessibility.Public)
        {
            continue;
        }

        switch (member)
        {
            // Bindable: ConfigurationBinder walks public properties with a setter.
            case IPropertySymbol { SetMethod: not null, IsIndexer: false } property:
                info.Members.Add(new MemberInfo(
                    property.Name, property.Type.ToDisplayString(typeFormat), "property", true));
                break;

            case IFieldSymbol field when field.Type.SpecialType == SpecialType.System_String
                                         && (field.IsConst || field.IsStatic):
            {
                var value = field.HasConstantValue
                    ? field.ConstantValue as string
                    : LiteralInitializer(field);
                if (value is not null)
                {
                    for (var start = 0; start < path.Count; start++)
                    {
                        AddConstant(string.Join(".", path.Skip(start).Append(field.Name)), value);
                    }
                    AddConstant(field.Name, value);
                }
                break;
            }

            // NOT bindable: ConfigurationBinder ignores fields. Recorded so a key that exists
            // in a store but can never take effect is reported as such, rather than looking
            // like an extraction miss.
            case IFieldSymbol { IsConst: false, IsStatic: false, IsImplicitlyDeclared: false } field:
                info.Members.Add(new MemberInfo(
                    field.Name, field.Type.ToDisplayString(typeFormat), "field", false));
                break;
        }
    }
}

// Local constants inside a method body, including the top-level statements of a Program.cs.
// Automation.UI declares `const string ApiBearerConfigSection = "Authentication:ApiBearer"`
// this way and then interpolates it, so without these the keys it reads look dead.
// Same ambiguity rule applies: a name declared twice with different values resolves to null.
foreach (var tree in trees)
{
    foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
    {
        if (!declaration.IsConst)
        {
            continue;
        }
        foreach (var variable in declaration.Declaration.Variables)
        {
            if (variable.Initializer?.Value is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                AddConstant(variable.Identifier.ValueText, literal.Token.ValueText);
            }
        }
    }
}

IEnumerable<string> Bases(INamedTypeSymbol type)
{
    for (var b = type.BaseType; b is not null && b.SpecialType != SpecialType.System_Object; b = b.BaseType)
    {
        if (b.Locations.Any(l => l.IsInSource))
        {
            yield return b.Name;
        }
    }
}

static string? LiteralInitializer(IFieldSymbol field)
{
    foreach (var reference in field.DeclaringSyntaxReferences)
    {
        if (reference.GetSyntax() is VariableDeclaratorSyntax
            {
                Initializer.Value: LiteralExpressionSyntax literal
            }
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }
    }
    return null;
}

// A name that resolves to more than one value is emitted as null: better unresolved than
// wrong, since guessing files a whole section's keys under the wrong name.
var constantMap = constants.ToDictionary(
    kv => kv.Key,
    kv => kv.Value.Count == 1 ? kv.Value.Single() : null);

var payload = new
{
    generatedBy = "Scripts/AzureAppConfig/dump_config_symbols.cs",
    sourceRoot = root,
    fileCount = sources.Count,
    types = types.ToDictionary(kv => kv.Key, kv => new
    {
        bases = kv.Value.Bases,
        members = kv.Value.Members.Select(m => new
        {
            name = m.Name, type = m.Type, kind = m.Kind, bindable = m.Bindable,
        }),
    }),
    constants = constantMap,
};

Directory.CreateDirectory(Path.GetDirectoryName(outputPath) is { Length: > 0 } dir ? dir : ".");
File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
}));

var ambiguous = constantMap.Count(kv => kv.Value is null);
Console.Error.WriteLine(
    $"Wrote {outputPath}: {types.Count} types, {constantMap.Count} constant names " +
    $"({ambiguous} ambiguous), {types.Values.Sum(t => t.Members.Count)} members.");

sealed class TypeInfo
{
    public List<string> Bases { get; } = new();
    public List<MemberInfo> Members { get; } = new();
}

record MemberInfo(string Name, string Type, string Kind, bool Bindable);
