using LantanaGroup.Link.Shared.Application.Extensions;
using Quartz;
using System.Text.Json;

namespace UnitTests.Shared;

public class JobDataMapExtensionsTests
{
    private enum TestStatus
    {
        Unknown = 0,
        Active = 1,
        Complete = 2
    }

    private sealed record ComplexPayload(Guid Id, string Name, int Count, bool Enabled);

    [Fact]
    public void PutObject_String_StoresRawString()
    {
        var map = new JobDataMap();

        map.PutObject("name", "alpha");

        Assert.Equal("alpha", map["name"]);
    }

    [Fact]
    public void PutObject_Null_StoresNull()
    {
        var map = new JobDataMap();

        map.PutObject<string?>("name", null);

        Assert.True(map.ContainsKey("name"));
        Assert.Null(map["name"]);
    }

    [Fact]
    public void PutObject_BoxedGuid_And_GetObjectNullableGuid_RoundTrips()
    {
        var map = new JobDataMap();
        var id = Guid.NewGuid();
        object boxed = id;

        map.PutObject("id", boxed);

        var resolved = map.GetObject<Guid?>("id");
        Assert.Equal(id, resolved);
    }

    [Fact]
    public void PutObject_ComplexObject_And_GetObject_RoundTrips()
    {
        var map = new JobDataMap();
        var payload = new ComplexPayload(Guid.NewGuid(), "demo", 7, true);

        map.PutObject("payload", payload);

        var resolved = map.GetObject<ComplexPayload>("payload");
        Assert.NotNull(resolved);
        Assert.Equal(payload, resolved);
    }

    [Fact]
    public void GetObject_MissingKey_ReturnsDefault()
    {
        var map = new JobDataMap();

        var resolved = map.GetObject<Guid?>("missing");

        Assert.Null(resolved);
    }

    [Fact]
    public void GetObject_DirectTypedValue_ReturnsValue()
    {
        var map = new JobDataMap
        {
            ["id"] = Guid.NewGuid()
        };

        var resolved = map.GetObject<Guid?>("id");

        Assert.Equal((Guid)map["id"], resolved);
    }

    [Fact]
    public void GetObject_StringFromJsonQuoted_UnwrapsForStringType()
    {
        var map = new JobDataMap
        {
            ["name"] = "\"quoted\""
        };

        var resolved = map.GetObject<string>("name");

        Assert.Equal("quoted", resolved);
    }

    [Fact]
    public void GetObject_PlainString_ForStringType_ReturnsPlainString()
    {
        var map = new JobDataMap
        {
            ["name"] = "plain"
        };

        var resolved = map.GetObject<string>("name");

        Assert.Equal("plain", resolved);
    }

    [Fact]
    public void GetObject_WhitespaceString_ReturnsDefaultForNonString()
    {
        var map = new JobDataMap
        {
            ["id"] = "   "
        };

        var resolved = map.GetObject<Guid?>("id");

        Assert.Null(resolved);
    }

    [Fact]
    public void GetObject_GuidSerializedString_ParsesGuid()
    {
        var id = Guid.NewGuid();
        var map = new JobDataMap
        {
            ["id"] = JsonSerializer.Serialize(id)
        };

        var resolved = map.GetObject<Guid?>("id");

        Assert.Equal(id, resolved);
    }

    [Fact]
    public void GetObject_DateTimeOffsetSerializedString_ParsesDateTimeOffset()
    {
        var value = DateTimeOffset.UtcNow;
        var map = new JobDataMap
        {
            ["dto"] = JsonSerializer.Serialize(value)
        };

        var resolved = map.GetObject<DateTimeOffset?>("dto");

        Assert.NotNull(resolved);
        Assert.Equal(value.ToUnixTimeSeconds(), resolved.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void GetObject_DateTimeSerializedString_ParsesDateTime()
    {
        var value = DateTime.UtcNow;
        var map = new JobDataMap
        {
            ["dt"] = JsonSerializer.Serialize(value)
        };

        var resolved = map.GetObject<DateTime?>("dt");

        Assert.NotNull(resolved);
        Assert.Equal(value.ToString("O"), resolved.Value.ToString("O"));
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("active")]
    [InlineData("\"Active\"")]
    public void GetObject_EnumFromString_ParsesCaseInsensitive(string raw)
    {
        var map = new JobDataMap
        {
            ["status"] = raw
        };

        var resolved = map.GetObject<TestStatus?>("status");

        Assert.Equal(TestStatus.Active, resolved);
    }

    [Fact]
    public void GetObject_InvalidEnum_ReturnsDefault()
    {
        var map = new JobDataMap
        {
            ["status"] = "NotARealValue"
        };

        var resolved = map.GetObject<TestStatus?>("status");

        Assert.Null(resolved);
    }

    [Fact]
    public void GetObject_JsonElement_Deserializes()
    {
        var value = new ComplexPayload(Guid.NewGuid(), "jsonElement", 3, false);
        var map = new JobDataMap
        {
            ["payload"] = JsonSerializer.SerializeToElement(value)
        };

        var resolved = map.GetObject<ComplexPayload>("payload");

        Assert.NotNull(resolved);
        Assert.Equal(value, resolved);
    }

    [Fact]
    public void GetObject_ConvertChangeTypeFallback_WorksForInt()
    {
        var map = new JobDataMap
        {
            ["count"] = (short)12
        };

        var resolved = map.GetObject<int>("count");

        Assert.Equal(12, resolved);
    }

    [Fact]
    public void GetObject_UnconvertibleValue_ReturnsDefault()
    {
        var map = new JobDataMap
        {
            ["id"] = new object()
        };

        var resolved = map.GetObject<Guid?>("id");

        Assert.Null(resolved);
    }

    [Fact]
    public void PutObject_GenericGuidNullable_NullValue_RetrievesNull()
    {
        var map = new JobDataMap();
        Guid? id = null;

        map.PutObject("id", id);

        var resolved = map.GetObject<Guid?>("id");
        Assert.Null(resolved);
    }
}
