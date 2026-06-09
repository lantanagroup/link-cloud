using LantanaGroup.Link.DataAcquisition.Domain.Application.Attributes;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using System.ComponentModel.DataAnnotations;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class ValidateQueryPlanConfigDictionaryAttributeTests
{
    private const string InvalidOperationTypeError = "Invalid OperationType value for ParameterQueryConfig.";
    private const string ReadNotValidError = "'Read' is not a valid operation type for ParameterQueryConfig.";

    private readonly ValidateQueryPlanConfigDictionaryAttribute _attribute = new();

    private static Dictionary<string, IQueryConfig> BuildDictionary(OperationType operationType)
    {
        return new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                OperationType = operationType,
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "_id", Literal = "abc" }
                }
            }
        };
    }

    private ValidationResult? Validate(Dictionary<string, IQueryConfig> dictionary)
    {
        var context = new ValidationContext(dictionary) { DisplayName = "TestQueries" };
        return _attribute.GetValidationResult(dictionary, context);
    }

    [Theory]
    [InlineData(OperationType.Search)]
    [InlineData(OperationType.SearchPost)]
    public void Validate_ParameterQueryConfig_WithValidOperationType_DoesNotAddOperationTypeError(OperationType operationType)
    {
        // Arrange
        var dictionary = BuildDictionary(operationType);

        // Act
        var result = Validate(dictionary);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void Validate_ParameterQueryConfig_WithReadOperationType_AddsReadNotValidError()
    {
        // Arrange
        var dictionary = BuildDictionary(OperationType.Read);

        // Act
        var result = Validate(dictionary);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(ReadNotValidError, result!.ErrorMessage);
        Assert.DoesNotContain(InvalidOperationTypeError, result.ErrorMessage);
    }

    [Fact]
    public void Validate_ParameterQueryConfig_WithUndefinedOperationType_AddsInvalidOperationTypeError()
    {
        // Arrange — Read=0, Search=1, SearchPost=2; 999 is not a defined member
        var dictionary = BuildDictionary((OperationType)999);

        // Act
        var result = Validate(dictionary);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(InvalidOperationTypeError, result!.ErrorMessage);
    }

    [Fact]
    public void Validate_ParameterQueryConfig_WithUndefinedOperationType_DoesNotAlsoAddReadError()
    {
        // Arrange — verifies the `else if` gate: undefined value must not also trip the Read-specific message
        var dictionary = BuildDictionary((OperationType)999);

        // Act
        var result = Validate(dictionary);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain(ReadNotValidError, result!.ErrorMessage);
    }
}
