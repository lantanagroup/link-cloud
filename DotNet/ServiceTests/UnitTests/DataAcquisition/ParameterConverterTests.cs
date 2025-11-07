using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using Xunit;

namespace UnitTests.DataAcquisition.Converters;

[Trait("Category", "UnitTests")]
public class ParameterConverterTests
{
    private readonly JsonSerializerOptions _options;

    public ParameterConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new ParameterConverter());
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public void Deserialize_OldLiteralParameter_WithType_ShouldReturnLiteralParameter()
    {
        string json = """
        {
            "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain",
            "Name": "category",
            "Literal": "imaging,laboratory,social-history,vital-signs"
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<LiteralParameter>(param);
        var literalParam = (LiteralParameter)param;
        Assert.Equal("category", literalParam.Name);
        Assert.Equal("imaging,laboratory,social-history,vital-signs", literalParam.Literal);
        Assert.Equal(ParameterType.Literal, literalParam.ParameterType);
    }

    [Fact]
    public void Deserialize_OldResourceIdsParameter_WithType_ShouldReturnResourceIdsParameter()
    {
        string json = """
        {
            "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain",
            "Name": "encounter",
            "Resource": "Encounter",
            "Paged": "100"
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<ResourceIdsParameter>(param);
        var resourceIdsParam = (ResourceIdsParameter)param;
        Assert.Equal("encounter", resourceIdsParam.Name);
        Assert.Equal("Encounter", resourceIdsParam.Resource);
        Assert.Equal("100", resourceIdsParam.Paged);
        Assert.Equal(ParameterType.ResourceIds, resourceIdsParam.ParameterType);
    }

    [Fact]
    public void Deserialize_OldVariableParameter_WithType_ShouldReturnVariableParameter()
    {
        string json = """
        {
            "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
            "Name": "patient",
            "Variable": "PatientId",
            "Format": null
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<VariableParameter>(param);
        var variableParam = (VariableParameter)param;
        Assert.Equal("patient", variableParam.Name);
        Assert.Equal(Variable.PatientId, variableParam.Variable);
        Assert.Null(variableParam.Format);
        Assert.Equal(ParameterType.Variable, variableParam.ParameterType);
    }

    [Fact]
    public void Deserialize_NewLiteralParameter_WithParameterType_ShouldReturnLiteralParameter()
    {
        string json = """
        {
            "ParameterType": "Literal",
            "Name": "category",
            "Literal": "imaging,laboratory,social-history,vital-signs"
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<LiteralParameter>(param);
        var literalParam = (LiteralParameter)param;
        Assert.Equal("category", literalParam.Name);
        Assert.Equal("imaging,laboratory,social-history,vital-signs", literalParam.Literal);
        Assert.Equal(ParameterType.Literal, literalParam.ParameterType);
    }

    [Fact]
    public void Deserialize_NewResourceIdsParameter_WithParameterType_ShouldReturnResourceIdsParameter()
    {
        string json = """
        {
            "ParameterType": "ResourceIds",
            "Name": "encounter",
            "Resource": "Encounter",
            "Paged": "100"
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<ResourceIdsParameter>(param);
        var resourceIdsParam = (ResourceIdsParameter)param;
        Assert.Equal("encounter", resourceIdsParam.Name);
        Assert.Equal("Encounter", resourceIdsParam.Resource);
        Assert.Equal("100", resourceIdsParam.Paged);
        Assert.Equal(ParameterType.ResourceIds, resourceIdsParam.ParameterType);
    }

    [Fact]
    public void Deserialize_NewVariableParameter_WithParameterType_ShouldReturnVariableParameter()
    {
        string json = """
        {
            "ParameterType": "Variable",
            "Name": "patient",
            "Variable": "PatientId",
            "Format": null
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<VariableParameter>(param);
        var variableParam = (VariableParameter)param;
        Assert.Equal("patient", variableParam.Name);
        Assert.Equal(Variable.PatientId, variableParam.Variable);
        Assert.Null(variableParam.Format);
        Assert.Equal(ParameterType.Variable, variableParam.ParameterType);
    }

    [Fact]
    public void Deserialize_LiteralParameter_WithoutTypeInfo_UsingProperties_ShouldReturnLiteralParameter()
    {
        string json = """
        {
            "Name": "category",
            "Literal": "imaging,laboratory,social-history,vital-signs"
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<LiteralParameter>(param);
        var literalParam = (LiteralParameter)param;
        Assert.Equal("category", literalParam.Name);
        Assert.Equal("imaging,laboratory,social-history,vital-signs", literalParam.Literal);
        Assert.Equal(ParameterType.Literal, literalParam.ParameterType);
    }

    [Fact]
    public void Deserialize_ResourceIdsParameter_WithoutTypeInfo_UsingProperties_ShouldReturnResourceIdsParameter()
    {
        string json = """
        {
            "Name": "encounter",
            "Resource": "Encounter",
            "Paged": "100"
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<ResourceIdsParameter>(param);
        var resourceIdsParam = (ResourceIdsParameter)param;
        Assert.Equal("encounter", resourceIdsParam.Name);
        Assert.Equal("Encounter", resourceIdsParam.Resource);
        Assert.Equal("100", resourceIdsParam.Paged);
        Assert.Equal(ParameterType.ResourceIds, resourceIdsParam.ParameterType);
    }

    [Fact]
    public void Deserialize_VariableParameter_WithoutTypeInfo_UsingProperties_ShouldReturnVariableParameter()
    {
        string json = """
        {
            "Name": "patient",
            "Variable": "PatientId",
            "Format": null
        }
        """;

        IParameter param = JsonSerializer.Deserialize<IParameter>(json, _options);

        Assert.IsType<VariableParameter>(param);
        var variableParam = (VariableParameter)param;
        Assert.Equal("patient", variableParam.Name);
        Assert.Equal(Variable.PatientId, variableParam.Variable);
        Assert.Null(variableParam.Format);
        Assert.Equal(ParameterType.Variable, variableParam.ParameterType);
    }

    [Fact]
    public void Deserialize_UnknownParameterType_ShouldThrowJsonException()
    {
        string json = """
        {
            "ParameterType": "Unknown",
            "Name": "test"
        }
        """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IParameter>(json, _options));
    }

    [Fact]
    public void Deserialize_NoTypeDiscriminatorOrProperties_ShouldThrowJsonException()
    {
        string json = """
        {
            "Name": "test"
        }
        """;

        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IParameter>(json, _options));
        Assert.Contains("Unable to determine ParameterType", ex.Message);
    }

    [Fact]
    public void Serialize_LiteralParameter_ShouldWriteCorrectJson()
    {
        var param = new LiteralParameter
        {
            Name = "category",
            Literal = "imaging,laboratory,social-history,vital-signs"
        };

        string json = JsonSerializer.Serialize<IParameter>(param, _options);

        var expected = """
        {"ParameterType":"Literal","Name":"category","Literal":"imaging,laboratory,social-history,vital-signs"}
        """.Replace("\r\n", "").Replace("\n", "");

        Assert.Equal(expected, json);
    }

    [Fact]
    public void Serialize_ResourceIdsParameter_ShouldWriteCorrectJson()
    {
        var param = new ResourceIdsParameter
        {
            Name = "encounter",
            Resource = "Encounter",
            Paged = "100"
        };

        string json = JsonSerializer.Serialize<IParameter>(param, _options);

        var expected = """
        {"ParameterType":"ResourceIds","Name":"encounter","Resource":"Encounter","Paged":"100"}
        """.Replace("\r\n", "").Replace("\n", "");

        Assert.Equal(expected, json);
    }

    [Fact]
    public void Serialize_VariableParameter_ShouldWriteCorrectJson()
    {
        var param = new VariableParameter
        {
            Name = "patient",
            Variable = Variable.PatientId,
            Format = null
        };

        string json = JsonSerializer.Serialize<IParameter>(param, _options);

        var expected = """
        {"ParameterType":"Variable","Name":"patient","Variable":"PatientId","Format":null}
        """.Replace("\r\n", "").Replace("\n", "");

        Assert.Equal(expected, json);
    }

    [Fact]
    public void Deserialize_OldParametersList_ShouldSucceed()
    {
        string json = """
        [
            {
                "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.VariableParameter, DataAcquisition.Domain",
                "Name": "patient",
                "Variable": "PatientId",
                "Format": null
            },
            {
                "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.ResourceIdsParameter, DataAcquisition.Domain",
                "Name": "encounter",
                "Resource": "Encounter",
                "Paged": "100"
            },
            {
                "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter.LiteralParameter, DataAcquisition.Domain",
                "Name": "category",
                "Literal": "imaging,laboratory,social-history,vital-signs"
            }
        ]
        """;

        var list = JsonSerializer.Deserialize<List<IParameter>>(json, _options);

        Assert.Equal(3, list.Count);
        Assert.IsType<VariableParameter>(list[0]);
        Assert.IsType<ResourceIdsParameter>(list[1]);
        Assert.IsType<LiteralParameter>(list[2]);
    }

    [Fact]
    public void Deserialize_NewParametersList_ShouldSucceed()
    {
        string json = """
        [
            {
                "ParameterType": "Variable",
                "Name": "patient",
                "Variable": "PatientId",
                "Format": null
            },
            {
                "ParameterType": "ResourceIds",
                "Name": "encounter",
                "Resource": "Encounter",
                "Paged": "100"
            },
            {
                "ParameterType": "Literal",
                "Name": "category",
                "Literal": "imaging,laboratory,social-history,vital-signs"
            }
        ]
        """;

        var list = JsonSerializer.Deserialize<List<IParameter>>(json, _options);

        Assert.Equal(3, list.Count);
        Assert.IsType<VariableParameter>(list[0]);
        Assert.IsType<ResourceIdsParameter>(list[1]);
        Assert.IsType<LiteralParameter>(list[2]);
    }
}