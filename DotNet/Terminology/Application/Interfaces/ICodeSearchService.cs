using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
using LantanaGroup.Link.Terminology.Application.Models;

namespace LantanaGroup.Link.Terminology.Application.Interfaces;

/// <summary>
/// Searches the loaded terminology content for codes matching a caller's query.
/// </summary>
/// <remarks>
/// Separate from the FHIR operations in <c>FhirService</c>: this returns a Link read model rather than a
/// FHIR resource, and exists to serve the mapping UI's code picker.
/// </remarks>
public interface ICodeSearchService
{
    /// <summary>
    /// Runs a code search and returns one page of results.
    /// </summary>
    /// <param name="query">The sanitized query. See <see cref="CodeSearchQuery"/> for what the caller must have settled first.</param>
    /// <param name="cancellationToken">Token to indicate if the operation should be cancelled.</param>
    /// <returns>
    /// The matching page, with <c>metadata.totalCount</c> reporting the full match count before paging.
    /// A query that matches nothing returns an empty <c>records</c> array rather than an error.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// A named code system, value set or version is not loaded. <see cref="ArgumentException.ParamName"/>
    /// carries the query parameter the caller needs to correct.
    /// </exception>
    Task<PagedConfigModel<TerminologyCodeModel>> Search(CodeSearchQuery query, CancellationToken cancellationToken = default);
}
