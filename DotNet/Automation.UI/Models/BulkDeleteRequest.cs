namespace Automation.UI.Models;

public sealed class BulkDeleteRequest
{
    public List<Guid> Ids { get; set; } = [];
}
