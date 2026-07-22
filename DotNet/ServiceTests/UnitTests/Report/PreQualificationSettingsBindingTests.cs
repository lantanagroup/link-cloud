using LantanaGroup.Link.Report.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;

namespace UnitTests.Report;

/// <summary>
/// Pins the binding of pre-qualification.write-pre-qual-operation-outcome. Report deliberately reads
/// the Java/Spring dotted spelling of this key so that both runtimes read one literal string
/// (LEGLINK-466): Spring cannot bind a colon-separated key, and a dot is not a section separator in
/// .NET configuration, so the same string works on both sides. That makes this the one setting in the
/// service not bound via GetSection, so the binding is worth pinning rather than assuming.
/// </summary>
[Trait("Category", "UnitTests")]
public class PreQualificationSettingsBindingTests
{
    private const string Key = "pre-qualification.write-pre-qual-operation-outcome";
    private const string AppConfigKey = "/pre-qualification/write-pre-qual-operation-outcome";

    /// <summary>
    /// Mirrors the registration in Report's Program.cs. Kept in one place so the tests below exercise
    /// the real binding expression rather than a paraphrase of it.
    /// </summary>
    private static PreQualificationSettings Bind(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.Configure<PreQualificationSettings>(options =>
            options.WritePreQualOperationOutcome =
                configuration.GetValue<bool?>(PreQualificationSettings.AppConfigurationKey)
                ?? configuration.GetValue<bool>(PreQualificationSettings.WritePreQualOperationOutcomeKey));

        return services.BuildServiceProvider().GetRequiredService<IOptions<PreQualificationSettings>>().Value;
    }

    [Fact]
    public void ConstantsMatchTheKeysSharedWithTheValidationService()
    {
        // If either constant is "tidied" into a colon-separated .NET-style key, the two runtimes
        // silently stop reading the same setting. The slashed form is how the row is stored in Azure
        // App Configuration (matching /link/report/base-url); Spring maps it to the dotted form.
        Assert.Equal(AppConfigKey, PreQualificationSettings.AppConfigurationKey);
        Assert.Equal(Key, PreQualificationSettings.WritePreQualOperationOutcomeKey);
        Assert.Equal(Key, PreQualificationSettings.AppConfigurationKey.TrimStart('/').Replace('/', '.'));
    }

    [Fact]
    public void BindsFromTheSlashedAzureAppConfigurationKey()
    {
        // The single App Config row that also feeds the Java Validation service. The .NET provider
        // passes keys through verbatim, so Report must ask for the slashed name to see it.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AppConfigKey] = "true" })
            .Build();

        Assert.True(Bind(configuration).WritePreQualOperationOutcome);
    }

    [Fact]
    public void AppConfigurationKeyWinsOverTheLocalDottedKey()
    {
        // appsettings.json ships the dotted key defaulted to false. Without precedence, that static
        // default would mask an App Config row that turns the flag on in a deployed environment.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [Key] = "false",
                [AppConfigKey] = "true"
            })
            .Build();

        Assert.True(Bind(configuration).WritePreQualOperationOutcome);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void BindsFromJsonFlatKey(string configured, bool expected)
    {
        // The dotted key is a flat key, not nested objects: the JSON provider must surface it verbatim.
        var json = $$"""{ "{{Key}}": {{configured}} }""";
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        Assert.Equal(expected, Bind(configuration).WritePreQualOperationOutcome);
    }

    [Fact]
    public void BindsFromEnvironmentStyleKey()
    {
        // docker-compose sets this key by its dotted name on the report container, the same way it
        // sets it on the validation container.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "true" })
            .Build();

        Assert.True(Bind(configuration).WritePreQualOperationOutcome);
    }

    [Fact]
    public void DefaultsToFalseWhenAbsent()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.False(Bind(configuration).WritePreQualOperationOutcome);
    }

    [Fact]
    public void IsNotReadAsANestedSection()
    {
        // Guards the reason the dotted spelling is safe here: a nested PreQualification section is NOT
        // an alias for it. If someone "restores" the conventional shape, this fails rather than
        // silently leaving the flag stuck at false.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PreQualification:WritePreQualOperationOutcome"] = "true"
            })
            .Build();

        Assert.False(Bind(configuration).WritePreQualOperationOutcome);
    }
}
