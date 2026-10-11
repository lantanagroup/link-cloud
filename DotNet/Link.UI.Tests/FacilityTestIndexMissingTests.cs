using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class FacilityTestIndexMissingTests
{
    [Fact]
    public void FacilityAbsentFromTenantAnswerIsMissing()
    {
        var index = FacilityTestIndex.FromFlags(new[]
        {
            new FacilityFlag { FacilityId = "real", IsTest = false },
            new FacilityFlag { FacilityId = "test", IsTest = true },
        });

        Assert.False(index.IsMissing("real"));
        Assert.False(index.IsMissing("TEST"));
        Assert.True(index.IsMissing("removed"));
        Assert.True(index.IsTest("test"));
    }

    [Fact]
    public void UnreachableIndexReportsNothingMissing()
    {
        Assert.False(FacilityTestIndex.Unreachable.IsMissing("any"));
        Assert.False(FacilityTestIndex.None.IsMissing("any"));
    }
}