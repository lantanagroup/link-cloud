namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Marks an entity property whose value an audit event masks: a change to it is reported under
    /// its name with <see cref="LantanaGroup.Link.Shared.Application.Models.Configs.AuditOptions.MaskedValue"/>
    /// as both values, so a reader sees that it changed but never from what or to what. This is how
    /// personal data is kept out of the audit trail: a name or an email address is not a
    /// secret, so no shared name or pattern catches it, and only the person who owns the entity
    /// knows it is there.
    /// </summary>
    /// <example>
    /// <code>
    /// public class LinkUser : BaseEntityExtended
    /// {
    ///     [AuditMask] public string Email { get; set; }
    ///     [AuditMask] public string FirstName { get; set; }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class AuditMaskAttribute : Attribute
    {
    }
}
