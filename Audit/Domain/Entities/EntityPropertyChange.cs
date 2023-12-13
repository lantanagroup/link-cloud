namespace LantanaGroup.Link.Audit.Domain.Entities
{
    public class EntityPropertyChange
    {
        public string PropertyName { get; set; } = string.Empty;
        public string InitialPropertyValue { get; set; } = string.Empty;
        public string NewPropertyValue { get; set; } = string.Empty;
    }
}
