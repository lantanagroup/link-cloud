using System.Text.Json;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using LantanaGroup.Link.Submission.Application.Config;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Shared;

/// <summary>
/// Pins how the shipped appsettings files' <c>null</c> values reach the services. .NET 8 reads a JSON
/// <c>null</c> as an empty string; .NET 10 keeps it as <c>null</c>. The string expectations here are meant to
/// fail on the .NET 10 upgrade, which then changes them as a recorded decision.
/// </summary>
/// <remarks>
/// The files are linked into the test output by ServiceTests.csproj under Resources/&lt;service&gt;.
/// </remarks>
[Trait("Category", "UnitTests")]
public class ConfigurationNullBindingTests
{
    public static TheoryData<string, string, string[]> NullKeysByFile => new()
    {
        {
            "Submission",
            "appsettings.json",
            [
                "InternalBlobStorage:ConnectionString",
                "InternalBlobStorage:BlobContainerName",
                "InternalBlobStorage:BlobRoot",
                "ExternalBlobStorage:ConnectionString",
                "ExternalBlobStorage:BlobContainerName",
                "ExternalBlobStorage:BlobRoot"
            ]
        },
        {
            "MockFhirServer",
            "appsettings.json",
            ["MockFhirServer:GenerationSeed", "MockFhirServer:ClinicalPeriodStart", "MockFhirServer:ClinicalPeriodEnd"]
        },
        {
            "MockFhirServer",
            "appsettings.Docker.json",
            ["MockFhirServer:GenerationSeed", "MockFhirServer:ClinicalPeriodStart", "MockFhirServer:ClinicalPeriodEnd"]
        },
        {
            "Automation.UI",
            "appsettings.json",
            [
                "Automation:FhirQuery:MinAcquisitionPullTime",
                "Automation:FhirQuery:MaxAcquisitionPullTime",
                "Automation:FhirQuery:TimeZone"
            ]
        },
        {
            "Automation.UI",
            "appsettings.Docker.json",
            [
                "Automation:FhirQuery:MinAcquisitionPullTime",
                "Automation:FhirQuery:MaxAcquisitionPullTime",
                "Automation:FhirQuery:TimeZone"
            ]
        },
        {
            "NHSN-App-BFF",
            "appsettings.json",
            ["NhsnJwt:MaxTokenAgeMinutes"]
        }
    };

    public static TheoryData<string> AutomationUiFiles => new()
    {
        "appsettings.json",
        "appsettings.Docker.json"
    };

    [Theory]
    [MemberData(nameof(NullKeysByFile))]
    public void NullKeys_ShippedAppSettings_MatchPinnedList(string service, string file, string[] expected)
    {
        var actual = FindNullKeys(service, file);

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(NullKeysByFile))]
    public void RawValue_NullKeyInShippedAppSettings_ReadsAsEmptyString(string service,
                                                                        string file,
                                                                        string[] keys)
    {
        var configuration = Load(service, file);

        Assert.All(keys, key => Assert.Equal(string.Empty, configuration[key]));
    }

    [Theory]
    [InlineData(InternalBlobStorageSettings.Key)]
    [InlineData(ExternalBlobStorageSettings.Key)]
    public void Bind_SubmissionBlobStorage_StringsBindAsEmpty(string section)
    {
        var settings = new BlobStorageSettings();

        Load("Submission", "appsettings.json")
            .GetSection(section)
            .Bind(settings);

        Assert.Equal(string.Empty, settings.ConnectionString);
        Assert.Equal(string.Empty, settings.BlobContainerName);
        Assert.Equal(string.Empty, settings.BlobRoot);
    }

    [Theory]
    [MemberData(nameof(AutomationUiFiles))]
    public void Bind_AutomationFhirQuery_TimeSpansBindAsNullAndTimeZoneAsEmpty(string file)
    {
        var settings = new AutomationConfig();

        Load("Automation.UI", file)
            .GetSection("Automation")
            .Bind(settings);

        Assert.Null(settings.FhirQuery.MinAcquisitionPullTime);
        Assert.Null(settings.FhirQuery.MaxAcquisitionPullTime);
        Assert.Equal(string.Empty, settings.FhirQuery.TimeZone);
    }

    [Fact]
    public void Bind_NhsnJwtMaxTokenAgeMinutes_BindsAsNull()
    {
        var settings = new NhsnJwtSettings();

        Load("NHSN-App-BFF", "appsettings.json")
            .GetSection(NhsnJwtSettings.SectionName)
            .Bind(settings);

        Assert.Null(settings.MaxTokenAgeMinutes);
    }

    private static IConfiguration Load(string service, string file)
    {
        return new ConfigurationBuilder()
            .AddJsonFile(ResourcePath(service, file), optional: false)
            .Build();
    }

    private static List<string> FindNullKeys(string service, string file)
    {
        var options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        using var document = JsonDocument.Parse(File.ReadAllText(ResourcePath(service, file)), options);

        var keys = new List<string>();
        CollectNullKeys(document.RootElement, null, keys);
        return keys;
    }

    private static void CollectNullKeys(JsonElement element, string? path, List<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                keys.Add(path ?? string.Empty);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectNullKeys(property.Value, Join(path, property.Name), keys);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CollectNullKeys(item, Join(path, index.ToString()), keys);
                    index++;
                }
                break;
        }
    }

    private static string Join(string? path, string segment)
    {
        return path is null ? segment : $"{path}:{segment}";
    }

    private static string ResourcePath(string service, string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "Resources", service, file);
    }
}
