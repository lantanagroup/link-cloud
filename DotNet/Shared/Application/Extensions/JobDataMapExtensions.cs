using Quartz;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class JobDataMapExtensions
{
    /// <summary>
    /// Stores an object in the JobDataMap.
    /// Complex types are serialized to JSON.
    /// Strings are stored directly for compatibility with Quartz native storage.
    /// </summary>
    public static void PutObject<T>(this JobDataMap map, string key, T value)
    {
        map.PutObject(key, (object?)value);
    }

    public static void PutObject(this JobDataMap map, string key, object? value)
    {
        if (value == null)
        {
            map.Put(key, (string)null!);
            return;
        }

        if (value is string s)
        {
            map.Put(key, s);
            return;
        }

        var json = JsonSerializer.Serialize(value, value.GetType());
        map.Put(key, json);
    }

    /// <summary>
    /// Retrieves a value from the JobDataMap.
    /// Supports both native Quartz storage and JSON-serialized objects.
    /// Automatically unwraps JSON-quoted strings when retrieving as string.
    /// </summary>
    public static T? GetObject<T>(this JobDataMap map, string key)
    {
        if (!map.ContainsKey(key))
            return default;

        var storedValue = map[key];
        if (storedValue == null)
            return default;

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (targetType == typeof(string))
        {
            if (storedValue is string storedString)
            {
                try
                {
                    var deserialized = JsonSerializer.Deserialize<string>(storedString);
                    if (deserialized != null)
                        return (T)(object)deserialized;
                }
                catch
                {
                }

                var result = storedString;
                if (result.Length >= 2 && result[0] == '"' && result[^1] == '"')
                    result = result[1..^1];

                return (T)(object)result;
            }

            return (T)(object)storedValue.ToString()!;
        }

        if (storedValue is T directValue)
            return directValue;

        if (storedValue is JsonElement jsonElement)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            catch
            {
                return default;
            }
        }

        if (storedValue is string serializedString)
        {
            if (string.IsNullOrWhiteSpace(serializedString))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(serializedString);
            }
            catch
            {
                if (targetType == typeof(Guid) && Guid.TryParse(serializedString.Trim('"'), out var guid))
                    return (T)(object)guid;

                if (targetType == typeof(DateTimeOffset) && DateTimeOffset.TryParse(serializedString.Trim('"'), out var dto))
                    return (T)(object)dto;

                if (targetType == typeof(DateTime) && DateTime.TryParse(serializedString.Trim('"'), out var dt))
                    return (T)(object)dt;

                if (targetType.IsEnum)
                {
                    try
                    {
                        var enumValue = Enum.Parse(targetType, serializedString.Trim('"'), ignoreCase: true);
                        return (T)enumValue;
                    }
                    catch
                    {
                        return default;
                    }
                }
            }
        }

        try
        {
            var converted = Convert.ChangeType(storedValue, targetType);
            return (T)converted!;
        }
        catch
        {
            return default;
        }
    }
}