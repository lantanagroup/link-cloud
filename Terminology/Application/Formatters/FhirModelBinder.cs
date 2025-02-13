namespace Terminology.Application.Formatters;

using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.IO;
using Task = System.Threading.Tasks.Task;

public class FhirModelBinder : IModelBinder
{
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

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();

        var parser = new FhirJsonParser();
        var model = parser.Parse<Resource>(body);

        bindingContext.Result = ModelBindingResult.Success(model);
    }
}