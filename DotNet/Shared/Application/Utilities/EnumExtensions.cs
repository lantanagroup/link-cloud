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
                var x = value.GetAttributeOnEnum<StringValueAttribute>();
                if (x != null)
                {
                    retVal = value.GetAttributeOnEnum<StringValueAttribute>().StringValue;
                }
                else
                {
                    retVal = value?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                retVal = value?.ToString() ?? string.Empty;
            }

            return retVal;
        }
    }
}
