namespace LantanaGroup.Link.Tenant.Models.Messages
{
    public class PropertyChangeModel
    {
        public string PropertyName { get; set; } = string.Empty;
        public string InitialPropertyValue { get; set; } = string.Empty;
        public string NewPropertyValue { get; set; } = string.Empty;
    }
}
