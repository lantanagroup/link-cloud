using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class KafkaHeaderHelperTests
{
    [Fact]
    public void CopyMetricsMode_CopiesWhenPresent()
    {
        var source = new Headers();
        KafkaHeaderHelper.SetMetricsMode(source, "performance");
        var destination = new Headers
        {
            { KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes("c1") }
        };

        KafkaHeaderHelper.CopyMetricsMode(source, destination);

        Assert.Equal("performance", KafkaHeaderHelper.GetMetricsMode(destination));
        Assert.True(KafkaHeaderHelper.IsPerformanceMode(destination));
    }

    [Fact]
    public void CopyMetricsMode_LeavesDestinationUnchangedWhenSourceMissing()
    {
        var destination = new Headers();
        KafkaHeaderHelper.CopyMetricsMode(null, destination);
        KafkaHeaderHelper.CopyMetricsMode(new Headers(), destination);

        Assert.Null(KafkaHeaderHelper.GetMetricsMode(destination));
        Assert.False(KafkaHeaderHelper.IsPerformanceMode(destination));
    }
}
