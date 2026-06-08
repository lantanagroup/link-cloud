using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class RemoveExtensionsOperationServiceTests
{
    private const string UrlA = "http://example.com/fhir/extension/vendor-tag";
    private const string UrlB = "http://example.com/fhir/extension/internal-flag";
    private const string UrlC = "http://hl7.org/fhir/StructureDefinition/patient-mothersMaidenName";

    private readonly RemoveExtensionsOperationService _service;

    public RemoveExtensionsOperationServiceTests()
    {
        var logger = new Mock<ILogger<RemoveExtensionsOperationService>>().Object;
        _service = new RemoveExtensionsOperationService(logger);
    }

    private static Patient PatientWithExtensions(params string[] urls)
    {
        var patient = new Patient { Id = "test-patient" };
        foreach (var url in urls)
            patient.Extension.Add(new Extension(url, new FhirString("value")));
        return patient;
    }

    [Fact]
    public async Task RemoveExtensions_SingleMatchingUrl_RemovesExtension_ReturnsSuccess()
    {
        var patient = PatientWithExtensions(UrlA, UrlC);
        var operation = new RemoveExtensionsOperation
        {
            Name = "Remove vendor tag",
            ExtensionUrls = [UrlA]
        };

        var result = await _service.ProcessOperationAsync(operation, patient);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var modified = (Patient)result.Resource;
        Assert.Single(modified.Extension);
        Assert.Equal(UrlC, modified.Extension[0].Url);
    }

    [Fact]
    public async Task RemoveExtensions_MultipleMatchingUrls_AllRemoved_ReturnsSuccess()
    {
        var patient = PatientWithExtensions(UrlA, UrlB, UrlC);
        var operation = new RemoveExtensionsOperation
        {
            Name = "Remove vendor extensions",
            ExtensionUrls = [UrlA, UrlB]
        };

        var result = await _service.ProcessOperationAsync(operation, patient);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var modified = (Patient)result.Resource;
        Assert.Single(modified.Extension);
        Assert.Equal(UrlC, modified.Extension[0].Url);
    }

    [Fact]
    public async Task RemoveExtensions_AllExtensionsListed_AllRemoved_ReturnsSuccess()
    {
        var patient = PatientWithExtensions(UrlA, UrlB);
        var operation = new RemoveExtensionsOperation
        {
            Name = "Remove all",
            ExtensionUrls = [UrlA, UrlB]
        };

        var result = await _service.ProcessOperationAsync(operation, patient);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var modified = (Patient)result.Resource;
        Assert.Empty(modified.Extension);
    }

    [Fact]
    public async Task RemoveExtensions_NoMatchingUrls_ReturnsNoAction()
    {
        var patient = PatientWithExtensions(UrlC);
        var operation = new RemoveExtensionsOperation
        {
            Name = "Remove vendor tag",
            ExtensionUrls = [UrlA]
        };

        var result = await _service.ProcessOperationAsync(operation, patient);

        Assert.Equal(OperationStatus.NoAction, result.SuccessCode);
        var modified = (Patient)result.Resource;
        Assert.Single(modified.Extension);
        Assert.Equal(UrlC, modified.Extension[0].Url);
    }

    [Fact]
    public async Task RemoveExtensions_ResourceHasNoExtensions_ReturnsNoAction()
    {
        var patient = new Patient { Id = "test-patient" };
        var operation = new RemoveExtensionsOperation
        {
            Name = "Remove vendor tag",
            ExtensionUrls = [UrlA]
        };

        var result = await _service.ProcessOperationAsync(operation, patient);

        Assert.Equal(OperationStatus.NoAction, result.SuccessCode);
    }

    [Fact]
    public async Task RemoveExtensions_NullResource_ReturnsFailure()
    {
        var operation = new RemoveExtensionsOperation
        {
            Name = "Remove vendor tag",
            ExtensionUrls = [UrlA]
        };

        var result = await _service.ProcessOperationAsync(operation, null);

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
    }

    [Fact]
    public async Task RemoveExtensions_NullOperation_ReturnsFailure()
    {
        var patient = PatientWithExtensions(UrlA);

        var result = await _service.ProcessOperationAsync(null, patient);

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
    }

    [Fact]
    public async Task RemoveExtensions_UrlNotInList_ExtensionLeftIntact()
    {
        var patient = PatientWithExtensions(UrlA, UrlB, UrlC);
        var operation = new RemoveExtensionsOperation
        {
            Name = "Remove only A",
            ExtensionUrls = [UrlA]
        };

        var result = await _service.ProcessOperationAsync(operation, patient);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var modified = (Patient)result.Resource;
        Assert.Equal(2, modified.Extension.Count);
        Assert.DoesNotContain(modified.Extension, e => e.Url == UrlA);
        Assert.Contains(modified.Extension, e => e.Url == UrlB);
        Assert.Contains(modified.Extension, e => e.Url == UrlC);
    }
}
