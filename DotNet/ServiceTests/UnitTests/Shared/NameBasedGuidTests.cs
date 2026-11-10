using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.Shared;

/// <summary>
/// Covers <see cref="NameBasedGuid"/> against RFC 9562 version 5 values from an independent implementation.
/// </summary>
[Trait("Category", "UnitTests")]
public class NameBasedGuidTests
{
    private static readonly Guid DnsNamespace = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    private static readonly Guid UrlNamespace = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

    [Fact]
    public void CreateVersion5_DnsExample_MatchesReferenceValue()
    {
        // Python's uuid.uuid5(uuid.NAMESPACE_DNS, "www.example.com").
        var id = NameBasedGuid.CreateVersion5(DnsNamespace, "www.example.com");

        Assert.Equal(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"), id);
    }

    [Fact]
    public void CreateVersion5_NonAsciiName_HashesItAsUtf8()
    {
        // Python's uuid.uuid5(uuid.NAMESPACE_URL, "https://example.com/café").
        var id = NameBasedGuid.CreateVersion5(UrlNamespace, "https://example.com/café");

        Assert.Equal(new Guid("60c2a4cb-5b0b-59bf-a9a3-ad819ea730d0"), id);
    }

    [Fact]
    public void CreateVersion5_SameInputs_ReturnsSameId()
    {
        Assert.Equal(NameBasedGuid.CreateVersion5(DnsNamespace, "a"),
                     NameBasedGuid.CreateVersion5(DnsNamespace, "a"));
    }

    [Fact]
    public void CreateVersion5_DifferentNameOrNamespace_ReturnsDifferentId()
    {
        var baseline = NameBasedGuid.CreateVersion5(DnsNamespace, "a");

        Assert.NotEqual(baseline, NameBasedGuid.CreateVersion5(DnsNamespace, "b"));
        Assert.NotEqual(baseline, NameBasedGuid.CreateVersion5(UrlNamespace, "a"));
    }

    [Fact]
    public void CreateVersion5_AnyInput_SetsVersionAndVariantBits()
    {
        var text = NameBasedGuid.CreateVersion5(DnsNamespace, "anything").ToString();

        Assert.Equal('5', text[14]);
        Assert.Contains(text[19], "89ab");
    }

    [Fact]
    public void CreateVersion5_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NameBasedGuid.CreateVersion5(DnsNamespace, null!));
    }
}
