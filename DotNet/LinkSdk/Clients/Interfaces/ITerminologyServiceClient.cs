using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Terminology;

namespace LantanaGroup.Link.Sdk.Clients;

public interface ITerminologyServiceClient
{
    /// <summary>
    /// Searches the Terminology service's loaded code systems and value sets for codes.
    /// </summary>
    /// <remarks>
    /// Every parameter is optional on its own, but the request must constrain the result set: supply at
    /// least one of <paramref name="search"/>, <paramref name="codeSystem"/> or
    /// <paramref name="valueSet"/>. Supplying none of them, supplying both
    /// <paramref name="codeSystem"/> and <paramref name="valueSet"/>, or naming a code system, value set
    /// or version that is not loaded, all answer 400.
    /// </remarks>
    /// <param name="search">
    /// Free text, matched case-insensitively against both the code and its display; a code matching
    /// either is returned. Must be at least 3 characters when supplied.
    /// </param>
    /// <param name="codeSystem">Canonical URI restricting the search to one code system. Mutually exclusive with <paramref name="valueSet"/>.</param>
    /// <param name="valueSet">Canonical URI restricting the search to one value set. Mutually exclusive with <paramref name="codeSystem"/>.</param>
    /// <param name="version">
    /// The version of whichever of <paramref name="codeSystem"/> or <paramref name="valueSet"/> was
    /// supplied. The latest loaded version is used when omitted, and a version that is not loaded is
    /// refused rather than silently replaced with the latest.
    /// </param>
    /// <param name="excludeInactive">When true, omits codes whose resolved status is inactive. Inactive codes are returned by default.</param>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">
    /// The page size. The service clamps this to its maximum of 100, so a larger value returns 100
    /// records rather than an error.
    /// </param>
    /// <param name="cancellationToken">Token to indicate if the operation should be cancelled.</param>
    /// <returns>
    /// One page of matching codes. <c>Metadata.TotalCount</c> is the full match count before paging, so a
    /// count well above <paramref name="pageSize"/> is the signal to ask the user to refine rather than to
    /// walk every page. A search that matches nothing succeeds with an empty <c>Records</c> list.
    /// </returns>
    Task<LinkApiResponse<PagedConfigModel<TerminologyCodeModel>>> SearchCodesAsync(
        string? search = null,
        string? codeSystem = null,
        string? valueSet = null,
        string? version = null,
        bool excludeInactive = false,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
