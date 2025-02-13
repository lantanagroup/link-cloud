namespace Terminology.Application.Formatters;

using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

public class FhirOutputFormatter : TextOutputFormatter
{
    public FhirOutputFormatter()
    {
        SupportedMediaTypes.Add("application/fhir+json");
        SupportedMediaTypes.Add("application/json");
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    protected override bool CanWriteType(Type type)
    {
        return typeof(Resource).IsAssignableFrom(type);
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var response = context.HttpContext.Response;
        var serializer = new FhirJsonSerializer();
        var resource = context.Object as Resource;
        var responseBody = serializer.SerializeToString(resource);

        await response.WriteAsync(responseBody, selectedEncoding);
    }
}