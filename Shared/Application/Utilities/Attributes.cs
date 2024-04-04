namespace LantanaGroup.Link.Shared.Application.Utilities
{
    public class StringValueAttribute : Attribute
    {
        public string StringValue { get; set; }

        public StringValueAttribute(string value)
        {
            StringValue = value;
        }
    }
}
