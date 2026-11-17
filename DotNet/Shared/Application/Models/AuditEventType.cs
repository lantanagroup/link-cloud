using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models
{
    /// <summary>
    /// The action an audit event describes.
    /// </summary>
    /// <remarks>
    /// Serialised by name on AuditableEventOccurred - "Action":"Update", not "Action":1 - so the
    /// topic is readable on its own and a non-.NET producer needs no custom serialiser. The
    /// converter's allowIntegerValues default keeps the integer form readable, which it must stay:
    /// messages produced before this change, and anything sitting on -Retry or -Error, still carry
    /// it. The declared order is therefore still load-bearing; append new values, never reorder.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuditEventType
    {
        Create,
        Update,
        Delete,
        Query,
        Submit,
        Restore,
        SoftDelete
    }
}
