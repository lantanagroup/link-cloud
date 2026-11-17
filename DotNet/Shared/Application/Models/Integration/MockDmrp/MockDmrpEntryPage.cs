using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;

/// <summary>
/// One page of enrollment entries from the Mock DMRP API's support surface.
/// </summary>
public class MockDmrpEntryPage
{
    /// <summary>
    /// The entries on this page. Empty, not absent, when nothing matched.
    /// </summary>
    public List<MockDmrpEntryResponse> Records { get; set; } = [];

    /// <summary>
    /// Paging for the whole result, not just this page.
    /// </summary>
    public PaginationMetadata Metadata { get; set; } = new();
}
