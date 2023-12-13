namespace LantanaGroup.Link.DemoApiGateway.Application.models.audit
{
    public class PropertyChangeModel
    {
        public string PropertyName { get; set; } = string.Empty;
        public string InitialPropertyValue { get; set; } = string.Empty;
        public string NewPropertyValue { get; set; } = string.Empty;
    }
}
