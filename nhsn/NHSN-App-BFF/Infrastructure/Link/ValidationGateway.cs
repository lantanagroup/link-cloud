using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IValidationGateway over IValidationRawClient (see its own doc comment for why this isn't
// LinkSdk's IValidationServiceClient). The raw call returns Validation's JSON body untyped, so
// this gateway deserializes and maps it into the BFF's own PreQualIssue shape.
internal sealed class ValidationGateway : IValidationGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new() {PropertyNameCaseInsensitive = true};

    private static readonly CategoryWire UncategorizedCategory = new()
    {
        Title = "Uncategorized",
        Acceptable = false,
        Guidance = "These issues need to be categorized."
    };

    private readonly IValidationRawClient _validationClient;

    public ValidationGateway(IValidationRawClient validationClient)
    {
        _validationClient = validationClient;
    }

    public async Task<IReadOnlyList<PreQualIssue>> GetPatientResultsAsync(string facilityId, string reportId, string patientId, CancellationToken cancellationToken = default)
    {
        var body = await _validationClient.GetPatientResultsAsync(facilityId, reportId, patientId, "INFORMATION", cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var results = JsonSerializer.Deserialize<List<ResultWire>>(body, JsonOptions) ?? [];

        // A result with no category (Validation's own fallback -- see its Category.UNCATEGORIZED)
        // still needs somewhere to render; a result in more than one category appears once per
        // category, matching how the category breakdown counts it.
        return results
            .SelectMany(result =>
            {
                var categories = result.Categories is {Count: > 0} ? result.Categories : [UncategorizedCategory];
                return categories.Select(category => new PreQualIssue
                {
                    Category = new PreQualCategory
                    {
                        Title = category.Title,
                        Acceptable = category.Acceptable,
                        Guidance = category.Guidance
                    },
                    Message = result.Message ?? "",
                    Expression = result.Expression ?? "",
                    Location = result.Location ?? ""
                });
            })
            .ToList();
    }

    private sealed record ResultWire
    {
        public string? Message { get; init; }
        public string? Location { get; init; }
        public string? Expression { get; init; }
        public List<CategoryWire>? Categories { get; init; }
    }

    private sealed record CategoryWire
    {
        public string Title { get; init; } = "";
        public bool Acceptable { get; init; }
        public string Guidance { get; init; } = "";
    }
}
