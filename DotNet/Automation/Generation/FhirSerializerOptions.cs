using Hl7.Fhir.Model;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Provides pre-configured <see cref="JsonSerializerOptions"/> for FHIR R4 serialization
/// without depending on the Shared project.
/// </summary>
internal static class FhirSerializerOptions
{
    private static JsonSerializerOptions? _options;

    public static JsonSerializerOptions ForFhirWithoutValidation()
    {
        _options ??= InitializeOptions();
        return _options;
    }

    private static JsonSerializerOptions InitializeOptions()
    {
        var options = new JsonSerializerOptions();
        options.ForFhir(ModelInfo.ModelInspector);
        options.AllowTrailingCommas = true;
        options.PropertyNameCaseInsensitive = true;
        return options;
    }
}
