using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace LantanaGroup.Link.Terminology.Application.Formatters;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// A custom output formatter to handle serialization of FHIR resources
/// into JSON format as specified by the FHIR standard.
/// </summary>
/// <remarks>
/// This class supports the "application/fhir+json" and "application/json" media types.
/// It extends the <see cref="TextOutputFormatter"/> to provide custom serialization behavior
/// for FHIR resources.
/// </remarks>
public class FhirOutputFormatter : TextOutputFormatter
{
    /// <summary>
    /// Custom output formatter that serializes FHIR resources into JSON format
    /// according to the FHIR standard.
    /// </summary>
    /// <remarks>
    /// This formatter supports the media types "application/fhir+json" and "application/json".
    /// It provides functionality for handling types assignable to FHIR `Resource`.
    /// </remarks>
    public FhirOutputFormatter()
    {
        SupportedMediaTypes.Add("application/fhir+json");
        SupportedMediaTypes.Add("application/json");
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    /// <summary>
    /// Determines whether this formatter can write the specified type.
    /// </summary>
    /// <remarks>Any type assignable to the FHIR `Resource` is supported by this class.</remarks>
    /// <param name="type">The type of the object to evaluate.</param>
    /// <returns>True if the type can be written by this formatter; otherwise, false.</returns>
    protected override bool CanWriteType(Type? type)
    {
        if (type == null) return false;
        return typeof(Resource).IsAssignableFrom(type);
    }

    /// <summary>
    /// Writes the body of the HTTP response asynchronously, serializing a FHIR resource into the specified encoding.
    /// </summary>
    /// <param name="context">The context object containing details of the HTTP response and the object to serialize.</param>
    /// <param name="selectedEncoding">The character encoding to use for the response body.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var response = context.HttpContext.Response;
        var resource = context.Object as Resource;
        
        if (resource == null) return;
        
        var serializerOptions = new System.Text.Json.JsonSerializerOptions()
            .ForFhir()
            .UsingMode(DeserializerModes.Ostrich); 
        await System.Text.Json.JsonSerializer.SerializeAsync(response.Body, resource, serializerOptions);

        await response.Body.FlushAsync();
    }
}