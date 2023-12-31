﻿using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Commands;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace NormalizationTests;

public class OperationCommandTests
{
    [Fact]
    public async Task ApplyConceptMapCommand_Success() 
    {
        var logger = new Mock<ILogger<ApplyConceptMapHandler>>();

        var bundle = LoadTestBundle("TestBundle.json");
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
            Bundle = bundle,
            Operation = conceptMapOperation
        }, default);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ConditionalTransformationCommand_Success() 
    {
        var logger = new Mock<ILogger<ConditionalTransformationHandler>>();

        var bundle = LoadTestBundle("TestBundle.json");
        var conditionalTransformationOperation = new ConditionalTransformationOperation
        {
            FacilityId = "TestFacility",
            Name = "PeriodDateFixer",
            TransformResource = "Encounter",
            TransformElement = "Period",
            Conditions = new List<ConditionElement>()
        };

        var command = new ConditionalTransformationHandler(logger.Object);
        var result = await command.Handle(new ConditionalTransformationCommand
        {
            Bundle = bundle,
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

        var bundle = LoadTestBundle("TestBundle.json");
        var copyLocationIdentifierToTypeOperation = new CopyLocationIdentifierToTypeOperation
        {
            Name = "CopyLocationIdentifierToType",
        };

        var command = new CopyLocationIdentifierToTypeHandler();
        var result = await command.Handle(new CopyLocationIdentifierToTypeCommand
        {
            Bundle = bundle,
            PropertyChanges = new List<PropertyChangeModel>()
        }, default);

        Assert.NotNull(result);
    }

    private Bundle LoadTestBundle(string fileName)
    {
        //var path = Path.Combine("TestFiles", fileName);
        //var json = File.ReadAllText(path);
        var json = File.ReadAllText(fileName);
        return JsonSerializer.Deserialize<Bundle>(json, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
    }

    private string LoadTestConceptMap(string fileName)
    {
        //var path = Path.Combine("TestFiles", fileName);
        //return File.ReadAllText(path);
        return File.ReadAllText(fileName);
    }
}
