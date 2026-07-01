namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// Represents a code system code.
/// </summary>
public class CodeSystemCode : Code
{
    /// <summary>
    /// Gets or sets the status of the code system code.
    /// </summary>
    public CodeStatus Status { get; set; }
}