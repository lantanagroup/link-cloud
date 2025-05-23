using Confluent.Kafka;
using DataAcquisition.Listeners;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace LantanaGroup.Link.DataAcquisitionTests.Integration
{
    public class DataAcquisitionRequestedListenerTests
    {
        [Fact]
        public async Task ExecuteListenerAsync_ShouldProcessMessage()
        {
            var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
            var listener = new DataAcquisitionRequestedListener(mockServiceScopeFactory.Object);

            var consumeResult = new ConsumeResult<string, DataAcquisitionRequested>
            {
                Message = new Message<string, DataAcquisitionRequested>
                {
                    Value = new DataAcquisitionRequested()
                }
            };

            await listener.ExecuteListenerAsync(consumeResult, default);

            // Add assertions to verify behavior
        }
    }
}
