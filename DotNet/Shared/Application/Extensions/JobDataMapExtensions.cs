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
        if (value == null)
        {
            map.Put(key, (string)null!);
            return;
        }

        if (typeof(T) == typeof(string))
        {
            map.Put(key, (string)(object)value!);
            return;
        }

        string json = JsonSerializer.Serialize(value);
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

        if (typeof(T) == typeof(string))
        {
            if (storedValue is string rawValue && !string.IsNullOrEmpty(rawValue))
            {
                string? deserialized = JsonSerializer.Deserialize<string>(rawValue);
                if (deserialized != null)
                    return (T)(object)deserialized;

            }
            return default;
        }

        if (storedValue is T directValue)
            return directValue;

        if (storedValue is string storedString && !string.IsNullOrEmpty(storedString))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(storedString);
            }
            catch
            {
                return default;
            }
        }

        try
        {
            return (T)Convert.ChangeType(storedValue, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}