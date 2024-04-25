using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Commands;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace NormalizationTests;

public class OperationCommandTests
{
    [Fact]
    public async Task ApplyConceptMapCommand_Success() 
    {
        var logger = new Mock<ILogger<ApplyConceptMapHandler>>();

        var resource = LoadTestResource("TestResource.json");
        var conceptMapStr = LoadTestConceptMap("TestConceptMap.json");
        var conceptMap = JsonSerializer.Deserialize<ConceptMap>(conceptMapStr, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
        var conceptMapOperation = new ConceptMapOperation
        {
            FhirContext = "Encounter.class",
            FhirConceptMap = conceptMap
        };

        var command = new ApplyConceptMapHandler(logger.Object);
        var result = await command.Handle(new ApplyConceptMapCommand 
        {
            Resource = resource,
            Operation = conceptMapOperation
        }, default);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ConditionalTransformationCommand_Success() 
    {
        var logger = new Mock<ILogger<ConditionalTransformationHandler>>();

        var resource = LoadTestResource("TestResource.json");
        var conditionalTransformationOperation = new ConditionalTransformationOperation
        {
            FacilityId = "TestFacility",
            Name = "PeriodDateFixer",
            TransformResource = "Encounter",
            TransformElement = "Period",
            Conditions = new List<ConditionElement>()
        };

        var command = new ConditionalTransformationHandler(logger.Object, new ConditionalTransformationEvaluationService());
        var result = await command.Handle(new ConditionalTransformationCommand
        {
            Resource = resource,
            Operation = conditionalTransformationOperation,
            PropertyChanges = new List<PropertyChangeModel>()
        }, default);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task CopyElementCommand_Success() 
    {
        Assert.True(true);
    }

    [Fact]
    public async Task CopyLocationIdentifierToTypeCommand_Success() 
    {
        var logger = new Mock<ILogger<CopyLocationIdentifierToTypeHandler>>();

        var resource = LoadTestResource("TestResource.json");
        var copyLocationIdentifierToTypeOperation = new CopyLocationIdentifierToTypeOperation
        {
            Name = "CopyLocationIdentifierToType",
        };

        var command = new CopyLocationIdentifierToTypeHandler();
        var result = await command.Handle(new CopyLocationIdentifierToTypeCommand
        {
            Resource = resource,
            PropertyChanges = new List<PropertyChangeModel>()
        }, default);

        Assert.NotNull(result);
    }

    private Resource LoadTestResource(string fileName)
    {
        //var path = Path.Combine("TestFiles", fileName);
        //var json = File.ReadAllText(path);
        var json = File.ReadAllText(fileName);
        return JsonSerializer.Deserialize<Resource>(json, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
    }

    private string LoadTestConceptMap(string fileName)
    {
        //var path = Path.Combine("TestFiles", fileName);
        //return File.ReadAllText(path);
        return File.ReadAllText(fileName);
    }
}
