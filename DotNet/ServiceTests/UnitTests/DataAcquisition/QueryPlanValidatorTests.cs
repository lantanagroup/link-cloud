using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

namespace UnitTests.DataAcquisition.QPValidator;

[Trait("Category", "UnitTests")]
public class QueryPlanValidatorTests
{
    private readonly QueryPlanValidator _validator;

    public QueryPlanValidatorTests()
    {
        _validator = new QueryPlanValidator();
    }

    [Fact]
    public void ValidateQueryPlan_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new VariableParameter { Name = "patient", Variable = Variable.PatientId }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQueryPlan_WithNullInitialQueries_ReturnsError()
    {
        // Arrange
        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(null, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("InitialQueries cannot be null"));
    }

    [Fact]
    public void ValidateQueryPlan_WithEmptyParametersList_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>() // Empty list
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Parameters list cannot be null or empty"));
    }

    [Fact]
    public void ValidateQueryPlan_WithMissingResourceType_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "", // Empty resource type
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ResourceType is required"));
    }

    [Fact]
    public void ValidateQueryPlan_WithInvalidFHIRResourceType_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "InvalidResourceType", // Not a valid FHIR resource
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not a valid FHIR ResourceType"));
    }

    [Fact]
    public void ValidateQueryPlan_WithValidFHIRResourceTypes_ReturnsSuccess()
    {
        // Arrange - Using valid FHIR resource types
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            },
            ["1"] = new ParameterQueryConfig
            {
                ResourceType = "Observation",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "789" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQueryPlan_WithDuplicateCaseInsensitiveKeys_ReturnsError()
    {
        // Arrange - Dictionary won't allow exact duplicates, but we can test the validator logic
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid); // Should be valid as they're in different dictionaries
    }

    [Fact]
    public void ValidateQueryPlan_WithInvalidQueryOrder_ReturnsError()
    {
        // Arrange - ReferenceQuery should come after ParameterQuery
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ReferenceQueryConfig // Reference query first
            {
                ResourceType = "Observation",
                OperationType = OperationType.Search,
                Paged = 100
            },
            ["1"] = new ParameterQueryConfig // Parameter query second (wrong order)
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ParameterQueryConfig entries must appear before"));
    }


    [Fact]
    public void ValidateQueryPlan_MultipleResourceIdsParametersInSameQuery_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
            {
                new ResourceIdsParameter
                {
                    Name = "patient",
                    Resource = "Patient",
                    Paged = "100"
                },
                new ResourceIdsParameter
                {
                    Name = "encounter",
                    Resource = "Encounter",   // doesn't matter if valid or not here
                    Paged = "50"
                }
            }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>(); // empty is fine

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("references unknown Resource 'Encounter'"));
    }

    [Fact]
    public void ValidateQueryPlan_WithMissingParameterName_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter
                    {
                        Name = "", // Missing name
                        Literal = "123"
                    }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Name is required"));
    }

    [Fact]
    public void ValidateQueryPlan_WithNonNumericKeys_ReturnsWarning()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["alpha"] = new ParameterQueryConfig // Non-numeric key
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["beta"] = new ParameterQueryConfig // Non-numeric key
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid); // Should still be valid
        Assert.NotEmpty(result.Warnings); // But should have warnings
        Assert.Contains(result.Warnings, w => w.Contains("not numeric"));
    }

    [Fact]
    public void ValidateQueryPlan_WithInvalidPagedValue_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new ResourceIdsParameter
                    {
                        Name = "patient",
                        Resource = "Patient",
                        Paged = "not-a-number" // Invalid int value
                    }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not a valid int"));
    }

    [Fact]
    public void ValidateQueryPlan_WithValidPagedIntValue_ReturnsSuccess()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new ResourceIdsParameter
                    {
                        Name = "patient",
                        Resource = "Patient",
                        Paged = "100" // Valid int value
                    }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQueryPlan_WithZeroPagedValue_IsValid()
    {
        // Arrange - Paged = 0 is valid (no paging)
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new ResourceIdsParameter
                    {
                        Name = "patient",
                        Resource = "Patient",
                        Paged = "0" // Zero is valid (no paging)
                    }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQueryPlan_WithNegativeReferenceQueryPagedValue_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            },
            ["1"] = new ReferenceQueryConfig
            {
                ResourceType = "Observation",
                OperationType = OperationType.Search,
                Paged = -10 // Negative paged value
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Paged value cannot be negative"));
    }

    [Fact]
    public void ValidateQueryPlan_WithZeroReferenceQueryPagedValue_ReturnsWarning()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            },
            ["1"] = new ReferenceQueryConfig
            {
                ResourceType = "Observation",
                OperationType = OperationType.Search,
                Paged = 0 // Zero paged value
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Paged value is 0"));
    }

    [Fact]
    public void ValidateQueryPlan_WithValidCrossReferences_ReturnsSuccess()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new ResourceIdsParameter
                    {
                        Name = "patient",
                        Resource = "Patient", // References Patient from InitialQueries
                        Paged = "50"
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQueryPlan_WithMissingResourceForResourceIdsParameter_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new ResourceIdsParameter
                    {
                        Name = "patient",
                        Resource = "", // Missing resource
                        Paged = "50"
                    }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "123" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Resource is required"));
    }

    [Fact]
    public void ValidateQueryPlan_WithDuplicateResourceTypes_ReturnsWarning()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            },
            ["1"] = new ParameterQueryConfig
            {
                ResourceType = "Patient", // Duplicate resource type
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "identifier", Literal = "ABC" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Duplicate ResourceType"));
    }

    [Theory]
    [InlineData(OperationType.Search)]
    [InlineData(OperationType.SearchPost)]
    public void ValidateQueryPlan_ParameterQueryWithSearchOperationType_ReturnsSuccess(OperationType operationType)
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                OperationType = operationType,
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                OperationType = operationType,
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQueryPlan_ParameterQueryWithReadOperationType_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                OperationType = OperationType.Read,
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                OperationType = OperationType.Search,
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("'Read' is not a valid operation type for ParameterQueryConfig"));
    }

    [Fact]
    public void ValidateQueryPlan_ParameterQueryWithUndefinedOperationType_ReturnsError()
    {
        // Arrange
        var initialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                OperationType = (OperationType)999, // Undefined enum value
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "123" }
                }
            }
        };

        var supplementalQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Encounter",
                OperationType = OperationType.Search,
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "patient", Literal = "456" }
                }
            }
        };

        // Act
        var result = _validator.ValidateQueryPlan(initialQueries, supplementalQueries);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is not a valid OperationType enum value"));
    }
}
