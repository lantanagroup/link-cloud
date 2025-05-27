using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class OperationCommandTests
{
    [Fact]
    public async Task ApplyConceptMapCommand_Success()
    {
        var logger = new Mock<ILogger<NormalizationService>>();
        var conditionalTransformationEvaluationService = new Mock<IConditionalTransformationEvaluationService>();

        var resource = LoadTestResource("TestResource.json");
        var conceptMapStr = LoadTestConceptMap("TestConceptMap.json");
        var conceptMap = JsonSerializer.Deserialize<ConceptMap>(conceptMapStr, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
        var conceptMapOperation = new ConceptMapOperation
        {
            FhirContext = "Encounter.class",
            FhirConceptMap = conceptMap
        };

        var service = new NormalizationService(logger.Object, conditionalTransformationEvaluationService.Object);
        var result = await service.ApplyConceptMap(new ApplyConceptMapCommand
        {
            Resource = resource,
            Operation = conceptMapOperation
        }, default);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ConditionalTransformationCommand_Success()
    {
        var logger = new Mock<ILogger<NormalizationService>>();
        var conditionalTransformationEvaluationService = new Mock<IConditionalTransformationEvaluationService>();

        var resource = LoadTestResource("TestResource.json");
        var conditionalTransformationOperation = new ConditionalTransformationOperation
        {
            FacilityId = "TestFacility",
            Name = "PeriodDateFixer",
            TransformResource = "Encounter",
            TransformElement = "Period",
            Conditions = new List<ConditionElement>()
        };

        var service = new NormalizationService(logger.Object, conditionalTransformationEvaluationService.Object);

        var result = await service.ConditionalTransformation(new ConditionalTransformationCommand
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
        var logger = new Mock<ILogger<NormalizationService>>();
        var conditionalTransformationEvaluationService = new Mock<IConditionalTransformationEvaluationService>();

        var resource = LoadTestResource("TestResource.json");
        var copyLocationIdentifierToTypeOperation = new CopyLocationIdentifierToTypeOperation
        {
            Name = "CopyLocationIdentifierToType",
        };

        var service = new NormalizationService(logger.Object, conditionalTransformationEvaluationService.Object);
        var result = await service.CopyLocationIdentifierToType(new CopyLocationIdentifierToTypeCommand
        {
            Resource = resource,
            PropertyChanges = new List<PropertyChangeModel>()
        }, default);

        Assert.NotNull(result);
    }

    private Resource LoadTestResource(string fileName)
    {
        var path = Path.Combine("Resources", fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Resource>(json, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
    }

    private string LoadTestConceptMap(string fileName)
    {
        var path = Path.Combine("Resources", fileName);
        return File.ReadAllText(path);
    }
}
