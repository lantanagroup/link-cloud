namespace LantanaGroup.Link.Shared.Application.Models.Integration.Validation;

public class ValidationArtifactApiModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class ValidationCategoryApiModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public bool Acceptable { get; set; }
    public string? Guidance { get; set; }
}
