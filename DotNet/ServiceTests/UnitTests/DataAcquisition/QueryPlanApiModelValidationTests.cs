using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class QueryPlanApiModelValidationTests
{
    /// <summary>
    /// Runs the same DataAnnotations + IValidatableObject validation that MVC model binding runs,
    /// without the HTTP pipeline. validateAllProperties:true is required so property-level
    /// attributes (not just [Required]) are evaluated.
    /// </summary>
    private static (bool isValid, List<ValidationResult> results) Validate(QueryPlanApiModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return (isValid, results);
    }

    private static ParameterQueryConfig ValidConfig(string resourceType) => new()
    {
        ResourceType = resourceType,
        OperationType = OperationType.Search,
        Parameters = new List<IParameter> { new LiteralParameter { Name = "_id", Literal = "abc" } }
    };

    private static QueryPlanApiModel CreateValidModel() => new()
    {
        PlanName = "Test Plan",
        Type = Frequency.Monthly,
        FacilityId = "facility-1",
        EHRDescription = "An EHR",
        LookBack = "P30D",
        InitialQueries = new Dictionary<string, IQueryConfig> { ["0"] = ValidConfig("Patient") },
        SupplementalQueries = new Dictionary<string, IQueryConfig> { ["0"] = ValidConfig("Encounter") }
    };

    [Fact]
    public void ValidModel_PassesValidation()
    {
        var (isValid, results) = Validate(CreateValidModel());

        Assert.True(isValid, "Expected a fully-populated model to pass validation. Failures: " +
            string.Join("; ", results.Select(r => r.ErrorMessage)));
        Assert.Empty(results);
    }

    // ---- Property-level attribute rules ----

    [Fact]
    public void Type_Null_FailsRequired()
    {
        var model = CreateValidModel();
        model.Type = null;

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(QueryPlanApiModel.Type)) &&
            r.ErrorMessage!.Contains("Type is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanName_Null_FailsRequired()
    {
        var model = CreateValidModel();
        model.PlanName = null;

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(QueryPlanApiModel.PlanName)));
    }

    [Fact]
    public void PlanName_TooLong_FailsStringLength()
    {
        var model = CreateValidModel();
        model.PlanName = new string('x', 201);

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(QueryPlanApiModel.PlanName)) &&
            r.ErrorMessage!.Contains("between 1 and 200", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FacilityId_Null_FailsRequired()
    {
        var model = CreateValidModel();
        model.FacilityId = null;

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(QueryPlanApiModel.FacilityId)));
    }

    [Fact]
    public void InitialQueries_Empty_FailsMinDictionaryCount()
    {
        var model = CreateValidModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>();

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.ErrorMessage!.Contains("must contain at least one query configuration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InitialQueries_WithReadOperation_FailsConfigDictionaryAttribute()
    {
        var model = CreateValidModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                OperationType = OperationType.Read, // invalid for ParameterQueryConfig
                Parameters = new List<IParameter> { new LiteralParameter { Name = "_id", Literal = "abc" } }
            }
        };

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.ErrorMessage!.Contains("'Read' is not a valid operation type", StringComparison.OrdinalIgnoreCase));
    }

    // ---- IValidatableObject.Validate rules (only run once property-level validation passes) ----

    [Fact]
    public void LookBack_InvalidFormat_FailsObjectValidation()
    {
        var model = CreateValidModel();
        model.LookBack = "not-a-duration";

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(QueryPlanApiModel.LookBack)) &&
            r.ErrorMessage!.Contains("ISO 8601", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InitialQueries_NonNumericKeys_FailsObjectValidation()
    {
        var model = CreateValidModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig> { ["abc"] = ValidConfig("Patient") };

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(QueryPlanApiModel.InitialQueries)) &&
            r.ErrorMessage!.Contains("non-numeric keys", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InitialQueries_DuplicateResourceTypes_FailsObjectValidation()
    {
        var model = CreateValidModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            ["0"] = ValidConfig("Patient"),
            ["1"] = ValidConfig("Patient") // duplicate ResourceType
        };

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(QueryPlanApiModel.InitialQueries)) &&
            r.ErrorMessage!.Contains("duplicate ResourceType", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SupplementalQueries_NonNumericKeys_FailsObjectValidation()
    {
        var model = CreateValidModel();
        model.SupplementalQueries = new Dictionary<string, IQueryConfig> { ["abc"] = ValidConfig("Patient") };

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(QueryPlanApiModel.SupplementalQueries)) &&
            r.ErrorMessage!.Contains("non-numeric keys", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InitialQueries_NumericEquivalentKeys_FailsObjectValidation()
    {
        var model = CreateValidModel();
        model.InitialQueries = new Dictionary<string, IQueryConfig>
        {
            ["1"] = ValidConfig("Patient"),
            ["01"] = ValidConfig("Observation") // "01" parses to the same numeric key as "1"
        };

        var (isValid, results) = Validate(model);

        Assert.False(isValid);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(QueryPlanApiModel.InitialQueries)) &&
            r.ErrorMessage!.Contains("duplicate numeric key", StringComparison.OrdinalIgnoreCase));
    }
}
