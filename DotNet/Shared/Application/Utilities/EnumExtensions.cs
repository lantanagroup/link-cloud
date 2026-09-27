using System.Reflection;

namespace LantanaGroup.Link.Shared.Application.Utilities
{
    public static class EnumExtensions
    {
        public static string GetStringValue(this Enum value)
        {
            try
            {
                var attr = value.GetType()
                    .GetField(value.ToString())
                    ?.GetCustomAttribute<StringValueAttribute>();

                return attr?.StringValue ?? value.ToString() ?? string.Empty;
            }
            catch
            {
                return value?.ToString() ?? string.Empty;
            }
        }
    }
}
