namespace LantanaGroup.Link.Audit.Domain.Entities
{
    public class EntityPropertyChange
    {
        public string PropertyName { get; set; } = string.Empty;
        public string InitialPropertyValue { get; set; } = string.Empty;
        public string NewPropertyValue { get; set; } = string.Empty;

        /// <summary>
        /// The entity type and primary key of the row this change belongs to, so one event can
        /// describe an update that touched several rows.
        /// </summary>
        /// <remarks>
        /// Null on a change a handler added itself, and on every event stored before the producers
        /// recorded them - most of the table.
        /// </remarks>
        public string? Entity { get; set; }

        /// <inheritdoc cref="Entity"/>
        public string? RowKey { get; set; }
    }
}
