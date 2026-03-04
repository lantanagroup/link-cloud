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
    /// Supports both native Quartz storage (strings, primitives) and JSON-serialized complex objects.
    /// </summary>
    /// <typeparam name="T">Type to retrieve</typeparam>
    /// <returns>The value or default if not found or conversion fails</returns>
    public static T? GetObject<T>(this JobDataMap map, string key)
    {
        if (!map.ContainsKey(key))
            return default;

        var storedValue = map[key];

        if (storedValue is T directValue)
            return directValue;

        if (storedValue is string jsonString && !string.IsNullOrEmpty(jsonString))
        {
            if (typeof(T) == typeof(string))
                return (T)(object)jsonString;

            try
            {
                return JsonSerializer.Deserialize<T>(jsonString);
            }
            catch (JsonException)
            {
                if (typeof(T) == typeof(string))
                    return (T)(object)jsonString;

                throw;
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