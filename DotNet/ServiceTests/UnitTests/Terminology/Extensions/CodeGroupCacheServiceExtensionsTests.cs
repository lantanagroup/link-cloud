using LantanaGroup.Link.Terminology.Application.Extensions;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using Moq;
using Xunit;

namespace UnitTests.Terminology;

/// <summary>
/// Covers <see cref="CodeGroupCacheServiceExtensions.GetCodeGroupExact"/>, the guard that stops a caller
/// honouring an explicit version from being handed a different one.
/// </summary>
/// <remarks>
/// <c>ICodeGroupCacheService.GetCodeGroup</c> falls back to the latest cached version when the requested
/// one is absent, so "not loaded" and "loaded" are indistinguishable from its return value alone. Every
/// case below is about that fallback; the empty-version case in particular exists because treating a blank
/// version as a real one would make <c>$lookup</c> answer 404 where it answers 200 today.
/// </remarks>
public class CodeGroupCacheServiceExtensionsTests
{
    private const string SystemUri = "http://terminology.hl7.org/CodeSystem/v3-ActCode";

    private readonly Mock<ICodeGroupCacheService> _cache = new();

    private static CodeGroup Group(string version) => new()
    {
        Id = "v3-ActCode",
        Url = SystemUri,
        Version = version,
        Type = CodeGroup.CodeGroupTypes.CodeSystem
    };

    [Fact]
    public void GetCodeGroupExact_NoVersionRequested_ReturnsWhateverTheCacheGave()
    {
        _cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, null))
            .Returns(Group("2.0.0"));

        var result = _cache.Object.GetCodeGroupExact(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri);

        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public void GetCodeGroupExact_RequestedVersionIsTheCachedOne_ReturnsIt()
    {
        _cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, "1.0.0"))
            .Returns(Group("1.0.0"));

        var result = _cache.Object.GetCodeGroupExact(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, "1.0.0");

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.Version);
    }

    /// <summary>
    /// The case the guard exists for: the cache answers with the latest version rather than nothing, and
    /// without the re-check the caller would serve a version it was never asked for.
    /// </summary>
    [Fact]
    public void GetCodeGroupExact_RequestedVersionNotLoaded_ReturnsNullRatherThanTheLatest()
    {
        _cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, "9.9.9"))
            .Returns(Group("1.0.0"));

        var result = _cache.Object.GetCodeGroupExact(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, "9.9.9");

        Assert.Null(result);
    }

    [Fact]
    public void GetCodeGroupExact_VersionMatchIsCaseInsensitive()
    {
        _cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, "1.0.0-RC1"))
            .Returns(Group("1.0.0-rc1"));

        var result = _cache.Object.GetCodeGroupExact(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, "1.0.0-RC1");

        Assert.NotNull(result);
    }

    /// <summary>
    /// A blank version means "not supplied" everywhere else in this service, and the guard this replaced
    /// skipped its comparison for one. Comparing it would reject every group, turning a request that
    /// succeeds today into a not-found.
    /// </summary>
    [Fact]
    public void GetCodeGroupExact_BlankVersion_IsTreatedAsNotSupplied()
    {
        _cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, string.Empty))
            .Returns(Group("1.0.0"));

        var result = _cache.Object.GetCodeGroupExact(CodeGroup.CodeGroupTypes.CodeSystem, SystemUri, string.Empty);

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public void GetCodeGroupExact_NothingCached_ReturnsNull()
    {
        _cache.Setup(x => x.GetCodeGroup(
                It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((CodeGroup?)null);

        var result = _cache.Object.GetCodeGroupExact(CodeGroup.CodeGroupTypes.ValueSet, SystemUri, "1.0.0");

        Assert.Null(result);
    }
}
