using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace LantanaGroup.Link.Shared.Application.Utilities
{
    public static class EnumExtensions
    {
        public static string GetStringValue(this Enum value)
        {
            string retVal;

            try
            {
                retVal = value.GetAttributeOnEnum<StringValueAttribute>().StringValue;
            }
            catch
            {
                retVal = value?.ToString() ?? string.Empty;
            }

            return retVal;
        }
    }
}
