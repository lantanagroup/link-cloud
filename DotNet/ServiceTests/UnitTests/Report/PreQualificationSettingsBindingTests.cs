using LantanaGroup.Link.Report.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace UnitTests.Report;

/// <summary>
/// Pins the binding of PreQualification:WritePreQualOperationOutcome, and its correspondence with the
/// Java Validation service's counterpart key (LEGLINK-466).
/// <para>
/// The two runtimes deliberately read separate keys — Spring cannot bind a colon-separated key, and
/// the Java services are being retired, so a shared key would leave .NET carrying a translation layer
/// for a platform that no longer exists. The cost of that choice is that the two spellings can drift
/// apart silently, which is what these tests guard.
/// </para>
/// </summary>
[Trait("Category", "UnitTests")]
public class PreQualificationSettingsBindingTests
{
    private static PreQualificationSettings Bind(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.Configure<PreQualificationSettings>(
            configuration.GetSection(PreQualificationSettings.Key));

        return services.BuildServiceProvider().GetRequiredService<IOptions<PreQualificationSettings>>().Value;
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void BindsFromSection(string configured, bool expected)
    {
        // docker-compose sets PreQualification__WritePreQualOperationOutcome on the report container
        // and the environment provider maps '__' to ':', so this is also the deployed shape.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PreQualification:WritePreQualOperationOutcome"] = configured
            })
            .Build();

        Assert.Equal(expected, Bind(configuration).WritePreQualOperationOutcome);
    }

    [Fact]
    public void DefaultsToFalseWhenAbsent()
    {
        // The safe default: absent configuration leaves Report writing what it writes today.
        var configuration = new ConfigurationBuilder().Build();

        Assert.False(Bind(configuration).WritePreQualOperationOutcome);
    }

    [Fact]
    public void DotNetKeyCorrespondsToTheValidationServiceKey()
    {
        // The two runtimes read different keys for one decision, so the spellings have to stay in
        // correspondence. If the .NET section or property is renamed without renaming the Java
        // counterpart, the pair silently stops describing the same setting, and the resulting
        // half-configured state produces wrong NDJSON rather than an error.
        var dotNetKey = $"{PreQualificationSettings.Key}:{nameof(PreQualificationSettings.WritePreQualOperationOutcome)}";

        Assert.Equal(PreQualificationSettings.ValidationServiceKey, ToSpringKey(dotNetKey));
    }

    /// <summary>
    /// Converts "PreQualification:WritePreQualOperationOutcome" to
    /// "pre-qualification.write-pre-qual-operation-outcome": ':' becomes '.', and each PascalCase
    /// segment becomes kebab-case.
    /// </summary>
    private static string ToSpringKey(string dotNetKey)
    {
        return string.Join('.', dotNetKey.Split(':').Select(ToKebabCase));
    }

    private static string ToKebabCase(string segment)
    {
        return Regex.Replace(segment, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
    }
}
