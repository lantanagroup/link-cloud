namespace LantanaGroup.Link.Tests.E2ETests;

using System.Reflection;
using RestSharp;
using System;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Linq;
using Task = System.Threading.Tasks.Task;

public class MeasureLoader
{
    private readonly FhirJsonParser _parser = new FhirJsonParser();
    public string measureId;
    private Bundle evaluationBundle;
    private Bundle validationBundle;
    private readonly RestClient adminBffClient;

    public MeasureLoader(RestClient adminBffClient)
    {
        this.adminBffClient = adminBffClient;
    }

    private async Task<string> GetMeasureBundleJsonAsync()
    {
        if (TestConfig.MeasureBundleLocation.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = TestConfig.MeasureBundleLocation.Replace("file://", "", StringComparison.OrdinalIgnoreCase);
            return await File.ReadAllTextAsync(filePath);
        }
        else if (TestConfig.MeasureBundleLocation.StartsWith("resource://", StringComparison.OrdinalIgnoreCase))
        {
            var resourceName = TestConfig.MeasureBundleLocation
                .Replace("resource://", "", StringComparison.OrdinalIgnoreCase);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            
            if (stream == null)
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        else if (TestConfig.MeasureBundleLocation.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 TestConfig.MeasureBundleLocation.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var client = new RestClient();
            var request = new RestRequest(TestConfig.MeasureBundleLocation, Method.Get);
            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
                throw new Exception($"Failed to fetch bundle from {TestConfig.MeasureBundleLocation}: {response.ErrorMessage}");

            return response.Content;
        }

        throw new NotSupportedException($"Unsupported path type: {TestConfig.MeasureBundleLocation}");
    }

    private async Task GetMeasureBundleAsync()
    {
        var json = await this.GetMeasureBundleJsonAsync();
        var originalBundle = _parser.Parse<Bundle>(json);

        var evaluationTypes = new[] { "Measure", "Library", "ValueSet", "CodeSystem" };
        var validationTypes = new[] { "ImplementationGuide", "StructureDefinition", "SearchParameter", "ValueSet", "CodeSystem" };

        Measure measure = originalBundle.Entry.FirstOrDefault(e => e.Resource?.TypeName == "Measure")?.Resource as Measure ?? throw new InvalidOperationException("Measure not found in bundle.");
        this.measureId = measure.Id;
        
        this.evaluationBundle = new Bundle
        {
            Type = Bundle.BundleType.Transaction,
            Entry = originalBundle.Entry
                .Where(e => e.Resource != null && evaluationTypes.Contains(e.Resource.TypeName))
                .ToList()
        };

        this.validationBundle = new Bundle
        {
            Type = Bundle.BundleType.Transaction,
            Entry = originalBundle.Entry
                .Where(e => e.Resource != null && validationTypes.Contains(e.Resource.TypeName))
                .ToList()
        };
    }

    public async Task LoadAsync()
    {
        Console.WriteLine("Getting measure bundle...");
        await this.GetMeasureBundleAsync();
        
        Console.WriteLine("Loading measure bundle for evaluation...");
        var request = new RestRequest($"measure-definition/{this.measureId}", Method.Put);
        request.AddJsonBody(this.evaluationBundle.ToJson());
        var response = adminBffClient.ExecuteAsync(request);
        
        if (response.Result.StatusCode != System.Net.HttpStatusCode.OK)
        {
            Console.WriteLine($"Failed to load measure definition: {response.Result.Content}");
            throw new Exception("Failed to load measure definition.");
        }
    }
}