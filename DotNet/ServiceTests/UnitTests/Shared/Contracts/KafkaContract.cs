using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Serializes and deserializes Kafka messages the way every producer and consumer does, through the shared
/// <see cref="JsonWithFhirMessageSerializer{T}"/> and <see cref="JsonWithFhirMessageDeserializer{T}"/>, and
/// compares the result with a checked-in JSON fixture.
/// </summary>
/// <remarks>
/// The fixtures are in Resources/Contracts and are copied to the test output by ServiceTests.csproj.
/// </remarks>
public static class KafkaContract
{
    /// <summary>
    /// Serializes <paramref name="value"/> as a message value and asserts it matches the fixture.
    /// </summary>
    public static void AssertProducesFixture<T>(T value, string fixture)
    {
        var context = new SerializationContext(MessageComponentType.Value, "contract-test");
        var produced = new JsonWithFhirMessageSerializer<T>().Serialize(value, context);
        var expected = JsonNode.Parse(File.ReadAllText(FixturePath(fixture)));
        var actual = JsonNode.Parse(Encoding.UTF8.GetString(produced));

        Assert.True(JsonNode.DeepEquals(expected, actual),
                    $"Produced JSON does not match {fixture}.{Environment.NewLine}" +
                    $"Expected: {expected?.ToJsonString()}{Environment.NewLine}" +
                    $"Actual:   {actual?.ToJsonString()}");
    }

    /// <summary>
    /// Deserializes the fixture as a consumer of type <typeparamref name="T"/> would read it.
    /// </summary>
    public static T Consume<T>(string fixture)
    {
        var bytes = File.ReadAllBytes(FixturePath(fixture));
        var context = new SerializationContext(MessageComponentType.Value, "contract-test");
        var value = new JsonWithFhirMessageDeserializer<T>().Deserialize(bytes, false, context);

        Assert.NotNull(value);
        return value;
    }

    /// <summary>
    /// A UTC <see cref="DateTime"/>, the kind the producers put on the wire.
    /// </summary>
    public static DateTime Utc(string value)
    {
        return DateTime.Parse(value,
                              CultureInfo.InvariantCulture,
                              DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private static string FixturePath(string fixture)
    {
        return Path.Combine(AppContext.BaseDirectory, "Resources", "Contracts", fixture);
    }
}
