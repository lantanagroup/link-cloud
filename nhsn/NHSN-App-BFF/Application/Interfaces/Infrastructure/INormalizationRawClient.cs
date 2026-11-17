using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

/// <summary>
/// Normalization operations that don't fit LinkSdk's INormalizationServiceClient shape: that
/// client has SearchFacilityOperationsAsync/CreateOperationAsync/DeleteFacilityOperationsAsync but
/// no update method, even though Normalization's OperationsController exposes a real
/// PUT api/normalization/Operations. The implementation (NormalizationRawClient) still builds on
/// LinkSdk's LinkApiClientBase, the same base class the generated clients use, rather than
/// hand-rolling requests against a plain HttpClient.
/// </summary>
public interface INormalizationRawClient
{
    /// <summary>Updates an existing Normalization operation in place (PUT, not a patch — send every field).</summary>
    Task UpdateOperationAsync(UpdateNormalizationOperationRequestApiModel request, CancellationToken cancellationToken = default);
}
