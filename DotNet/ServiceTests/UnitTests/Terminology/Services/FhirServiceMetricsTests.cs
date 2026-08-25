using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;

namespace UnitTests.Terminology;

[Trait("Category", "UnitTests")]
public class FhirServiceMetricsTests
{
    [Fact]
    public void ValidateCodeInCodeSystem_NotFound_IncrementsLookupCountWithoutDuration()
    {
        var cache = new Mock<ICodeGroupCacheService>();
        var metrics = new Mock<ITerminologyServiceMetrics>();
        var service = new FhirService(cache.Object, Mock.Of<ILogger<FhirService>>(), metrics.Object);

        service.ValidateCodeInCodeSystem("http://example.org/CodeSystem/missing", null, "abc", null, null);

        metrics.Verify(m => m.IncrementLookupCount("not_found", "codesystem"), Times.Once);
        metrics.Verify(m => m.RecordLookupDuration(It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_PerformanceMode_RecordsDuration()
    {
        var cache = new Mock<ICodeGroupCacheService>();
        cache.Setup(c => c.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, "http://example.org/cs", null))
            .Returns(new CodeGroup
            {
                Type = CodeGroup.CodeGroupTypes.CodeSystem,
                Url = "http://example.org/cs",
                Codes = new Dictionary<string, List<Code>>
                {
                    ["http://example.org/cs"] = [new Code { Value = "abc", Display = "ABC" }]
                }
            });
        var metrics = new Mock<ITerminologyServiceMetrics>();
        var service = new FhirService(cache.Object, Mock.Of<ILogger<FhirService>>(), metrics.Object);

        using var scope = MetricsModeScope.Begin(true);
        service.ValidateCodeInCodeSystem("http://example.org/cs", null, "abc", null, null);

        metrics.Verify(m => m.IncrementLookupCount("success", "codesystem"), Times.Once);
        metrics.Verify(m => m.RecordLookupDuration(It.IsAny<double>(), "codesystem", "hit"), Times.Once);
    }
}
