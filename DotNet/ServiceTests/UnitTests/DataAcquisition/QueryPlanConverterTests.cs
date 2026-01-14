using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using Xunit;

namespace UnitTests.DataAcquisition.Converters;

[Trait("Category", "UnitTests")]
public class QueryPlanConverterTests
{
    private readonly JsonSerializerOptions _options;

    public QueryPlanConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new QueryConfigConverter());
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public void Deserialize_OldParameterQueryConfig_WithType_ShouldReturnParameterQueryConfig()
    {
        string json = """
        {
            "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
            "ResourceType": "Encounter",
            "Parameters": []
        }
        """;

        IQueryConfig config = JsonSerializer.Deserialize<IQueryConfig>(json, _options);

        Assert.IsType<ParameterQueryConfig>(config);
        var parameterConfig = (ParameterQueryConfig)config;
        Assert.Equal("Encounter", parameterConfig.ResourceType);
        Assert.Empty(parameterConfig.Parameters);
        Assert.Equal(QueryConfigType.Parameter, parameterConfig.QueryConfigType);
    }

    [Fact]
    public void Deserialize_OldReferenceQueryConfig_WithType_ShouldReturnReferenceQueryConfig()
    {
        string json = """
        {
            "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
            "ResourceType": "Location",
            "OperationType": 1,
            "Paged": 100
        }
        """;

        IQueryConfig config = JsonSerializer.Deserialize<IQueryConfig>(json, _options);

        Assert.IsType<ReferenceQueryConfig>(config);
        var referenceConfig = (ReferenceQueryConfig)config;
        Assert.Equal("Location", referenceConfig.ResourceType);
        Assert.Equal(OperationType.Search, referenceConfig.OperationType);
        Assert.Equal(100, referenceConfig.Paged);
        Assert.Equal(QueryConfigType.Reference, referenceConfig.QueryConfigType);
    }

    [Fact]
    public void Deserialize_NewParameterQueryConfig_WithQueryConfigType_ShouldReturnParameterQueryConfig()
    {
        string json = """
        {
            "QueryConfigType": "Parameter",
            "ResourceType": "Encounter",
            "Parameters": []
        }
        """;

        IQueryConfig config = JsonSerializer.Deserialize<IQueryConfig>(json, _options);

        Assert.IsType<ParameterQueryConfig>(config);
        var parameterConfig = (ParameterQueryConfig)config;
        Assert.Equal("Encounter", parameterConfig.ResourceType);
        Assert.Empty(parameterConfig.Parameters);
        Assert.Equal(QueryConfigType.Parameter, parameterConfig.QueryConfigType);
    }

    [Fact]
    public void Deserialize_NewReferenceQueryConfig_WithQueryConfigType_ShouldReturnReferenceQueryConfig()
    {
        string json = """
        {
            "QueryConfigType": "Reference",
            "ResourceType": "Location",
            "OperationType": 1,
            "Paged": 100
        }
        """;

        IQueryConfig config = JsonSerializer.Deserialize<IQueryConfig>(json, _options);

        Assert.IsType<ReferenceQueryConfig>(config);
        var referenceConfig = (ReferenceQueryConfig)config;
        Assert.Equal("Location", referenceConfig.ResourceType);
        Assert.Equal(OperationType.Search, referenceConfig.OperationType);
        Assert.Equal(100, referenceConfig.Paged);
        Assert.Equal(QueryConfigType.Reference, referenceConfig.QueryConfigType);
    }

    [Fact]
    public void Deserialize_ParameterQueryConfig_WithoutTypeInfo_UsingProperties_ShouldReturnParameterQueryConfig()
    {
        string json = """
        {
            "ResourceType": "Encounter",
            "Parameters": []
        }
        """;

        IQueryConfig config = JsonSerializer.Deserialize<IQueryConfig>(json, _options);

        Assert.IsType<ParameterQueryConfig>(config);
        var parameterConfig = (ParameterQueryConfig)config;
        Assert.Equal("Encounter", parameterConfig.ResourceType);
        Assert.Empty(parameterConfig.Parameters);
        Assert.Equal(QueryConfigType.Parameter, parameterConfig.QueryConfigType);
    }

    [Fact]
    public void Deserialize_ReferenceQueryConfig_WithoutTypeInfo_UsingProperties_ShouldReturnReferenceQueryConfig()
    {
        string json = """
        {
            "ResourceType": "Location",
            "OperationType": 1,
            "Paged": 100
        }
        """;

        IQueryConfig config = JsonSerializer.Deserialize<IQueryConfig>(json, _options);

        Assert.IsType<ReferenceQueryConfig>(config);
        var referenceConfig = (ReferenceQueryConfig)config;
        Assert.Equal("Location", referenceConfig.ResourceType);
        Assert.Equal(OperationType.Search, referenceConfig.OperationType);
        Assert.Equal(100, referenceConfig.Paged);
        Assert.Equal(QueryConfigType.Reference, referenceConfig.QueryConfigType);
    }

    [Fact]
    public void Deserialize_UnknownQueryConfigType_ShouldThrowJsonException()
    {
        string json = """
        {
            "QueryConfigType": "Unknown",
            "ResourceType": "Encounter"
        }
        """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IQueryConfig>(json, _options));
    }

    [Fact]
    public void Deserialize_NoTypeDiscriminatorOrProperties_ShouldThrowJsonException()
    {
        string json = """
        {
            "ResourceType": "Encounter"
        }
        """;

        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IQueryConfig>(json, _options));
        Assert.Contains("Unable to determine QueryConfigType", ex.Message);
    }

    [Fact]
    public void Serialize_ParameterQueryConfig_ShouldWriteCorrectJson()
    {
        var config = new ParameterQueryConfig
        {
            ResourceType = "Encounter",
            Parameters = new List<IParameter>()
        };

        string json = JsonSerializer.Serialize<IQueryConfig>(config, _options);

        var expected = """
        {"QueryConfigType":"Parameter","ResourceType":"Encounter","Parameters":[]}
        """.Replace("\r\n", "").Replace("\n", "");

        Assert.Equal(expected, json);
    }

    [Fact]
    public void Serialize_ReferenceQueryConfig_ShouldWriteCorrectJson()
    {
        var config = new ReferenceQueryConfig
        {
            ResourceType = "Location",
            OperationType = OperationType.Search,
            Paged = 100
        };

        string json = JsonSerializer.Serialize<IQueryConfig>(config, _options);

        var expected = """
        {"QueryConfigType":"Reference","ResourceType":"Location","OperationType":"Search","Paged":100}
        """.Replace("\r\n", "").Replace("\n", "");

        Assert.Equal(expected, json);
    }

    [Fact]
    public void Deserialize_OldFullInitialQueriesDictionary_ShouldSucceed()
    {
        // Simplified without full parameters, assuming ParameterConverter handles them
        string json = """
        {
            "0": {
                "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig, DataAcquisition.Domain",
                "ResourceType": "Encounter",
                "Parameters": []
            },
            "1": {
                "$type": "LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ReferenceQueryConfig, DataAcquisition.Domain",
                "ResourceType": "Location",
                "OperationType": 1,
                "Paged": 100
            }
        }
        """;

        var dict = JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(json, _options);

        Assert.Equal(2, dict.Count);
        Assert.IsType<ParameterQueryConfig>(dict["0"]);
        Assert.IsType<ReferenceQueryConfig>(dict["1"]);
    }

    [Fact]
    public void Deserialize_NewFullInitialQueriesDictionary_ShouldSucceed()
    {
        // Simplified without full parameters
        string json = """
        {
            "0": {
                "QueryConfigType": "Parameter",
                "ResourceType": "Encounter",
                "Parameters": []
            },
            "1": {
                "QueryConfigType": "Reference",
                "ResourceType": "Location",
                "OperationType": 1,
                "Paged": 100
            }
        }
        """;

        var dict = JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(json, _options);

        Assert.Equal(2, dict.Count);
        Assert.IsType<ParameterQueryConfig>(dict["0"]);
        Assert.IsType<ReferenceQueryConfig>(dict["1"]);
    }
}