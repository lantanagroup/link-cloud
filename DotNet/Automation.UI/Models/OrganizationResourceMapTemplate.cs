using System.ComponentModel.DataAnnotations;

namespace Automation.UI.Models;

public class OrganizationResourceMapTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsDefault { get; set; }
    public List<OrganizationResourceMapCondition> Conditions { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class OrganizationResourceMapCondition
{
    public string FhirPath { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
}
