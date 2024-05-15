using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Serializers;

public class NormalizationConfigModelDeserializer
{
    private static ConceptMapOperation DeserializeConceptMapOperation(JsonNode jsonNode, string facilityId)
    {
        var conceptMapOperation = new ConceptMapOperation();
        conceptMapOperation.FhirContext = jsonNode["FhirContext"]?.ToString();
        conceptMapOperation.FacilityId = facilityId;
        conceptMapOperation.FhirConceptMap = jsonNode["FhirConceptMap"];

        return conceptMapOperation;
    }

    public static NormalizationConfigModel Deserialize(dynamic configdyn)
    {
        var deserializedConfig = new NormalizationConfig();

        JsonElement configEle = (JsonElement)configdyn;
        var jsonNode = JsonNode.Parse(configEle.ToString());
        var facilityId = jsonNode["FacilityId"].ToString();
        var operationSequence = jsonNode["OperationSequence"];

        var operationSeqDict = new Dictionary<string, INormalizationOperation>();

        var incrementor = 0;
        while (true)
        {
            var incVal = incrementor.ToString();
            var operation = operationSequence[incVal];
            if(operation == null)
            {
                break;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            INormalizationOperation? deserializedOperation = operation["$type"]?.ToString() switch
            {
                "ConceptMapOperation" => DeserializeConceptMapOperation(operation, facilityId),
                "ConditionalTransformationOperation" => JsonSerializer.Deserialize<ConditionalTransformationOperation>(operation, options),
                "CopyElementOperation" => JsonSerializer.Deserialize<CopyElementOperation>(operation, options),
                "CopyLocationIdentifierToTypeOperation" => JsonSerializer.Deserialize<CopyLocationIdentifierToTypeOperation>(operation, options),
                "PeriodDateFixerOperation" => JsonSerializer.Deserialize<PeriodDateFixerOperation>(operation, options),
                _ => null,
            };

            if(deserializedOperation != null)
                operationSeqDict.Add(incrementor.ToString(), deserializedOperation); 
            
            incrementor++;
        }

        return new NormalizationConfigModel 
        {
            FacilityId = facilityId,
            OperationSequence = operationSeqDict
        };
    }
}
