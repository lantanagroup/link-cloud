using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// FHIR JSON serialization, indented so the hourly files can be read and edited by hand.
/// </summary>
public static class FhirJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>
    /// Serializes a resource to FHIR JSON.
    /// </summary>
    public static string Serialize(Resource resource) => JsonSerializer.Serialize(resource, resource.GetType(), Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
        options.WriteIndented = true;
        return options;
    }
}
