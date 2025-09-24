using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LantanaGroup.Link.Terminology.Application.Formatters;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// Provides custom model binding for FHIR (Fast Healthcare Interoperability Resources) resources.
/// </summary>
/// <remarks>
/// This model binder is designed to handle requests containing FHIR JSON payloads,
/// ensuring they are correctly parsed into FHIR resource types.
/// It supports requests with content types "application/fhir+json" or "application/json".
/// </remarks>
public class FhirModelBinder : IModelBinder
{
    /// <summary>
    /// Handles the binding of a FHIR resource model from the HTTP request body.
    /// </summary>
    /// <param name="bindingContext">The context for the model binding operation. Contains information such as the request, model name, and model metadata.</param>
    /// <returns>A task that represents the asynchronous model binding operation. The result of the task may indicate success or failure in binding the model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="bindingContext"/> is null.</exception>
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var request = bindingContext.HttpContext.Request;
        if (request.ContentType != "application/fhir+json" && request.ContentType != "application/json")
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        var serializerOptions = new JsonSerializerOptions()
            .ForFhir()
            .UsingMode(DeserializerModes.Ostrich);
        var model = await JsonSerializer.DeserializeAsync<Resource>(request.Body, serializerOptions);

        bindingContext.Result = ModelBindingResult.Success(model);
    }
}