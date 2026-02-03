using Quartz;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class JobDataMapExtensions
{
    /// <summary>
    /// Serializes the object to JSON and stores it as a string in the JobDataMap.
    /// </summary>
    /// <typeparam name="T">The type of the object to store.</typeparam>
    /// <param name="map">The JobDataMap instance.</param>
    /// <param name="key">The key to store under.</param>
    /// <param name="value">The object to serialize and store.</param>
    public static void PutObject<T>(this JobDataMap map, string key, T value)
    {
        if (value == null)
        {
            map.Put(key, (string)null!);  // Store null if value is null
            return;
        }

        string json = JsonSerializer.Serialize(value);
        map.Put(key, json);
    }

    /// <summary>
    /// Retrieves a string from the JobDataMap and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="map">The JobDataMap instance.</param>
    /// <param name="key">The key to retrieve from.</param>
    /// <returns>The deserialized object, or default(T) if not found or deserialization fails.</returns>
    /// <exception cref="JsonException">Thrown if deserialization fails (e.g., invalid JSON).</exception>
    public static T GetObject<T>(this JobDataMap map, string key)
    {
        var json = map.GetString(key);

        if (string.IsNullOrEmpty(json))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(json);
    }
}