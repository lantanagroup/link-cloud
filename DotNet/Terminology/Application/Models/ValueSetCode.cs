namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// Represents a value set member code that carries its own membership status.
/// </summary>
/// <remarks>
/// Value set membership status is independent of the underlying code system status: an intensional
/// value set is expanded from a code system, and a code can remain active in the code system yet be
/// dropped from the value set's membership. When a value set member is loaded as a <see cref="ValueSetCode"/>
/// its <see cref="Status"/> is authoritative and overrides the code system. Members loaded as a plain
/// <see cref="Code"/> (a value set file with no status column) fall back to the code system status.
/// </remarks>
public class ValueSetCode : Code
{
    /// <summary>
    /// Gets or sets the value set membership status of the code.
    /// </summary>
    public CodeStatus Status { get; set; } = CodeStatus.Active;
}
