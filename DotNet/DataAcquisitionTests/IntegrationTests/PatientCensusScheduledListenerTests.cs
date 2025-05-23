using Confluent.Kafka;
using DataAcquisition.Listeners;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace LantanaGroup.Link.DataAcquisitionTests.Integration
{
    public class PatientCensusScheduledListenerTests
    {
        [Fact]
        public async Task ExecuteListenerAsync_ShouldProcessMessage()
        {
            var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
            var listener = new PatientCensusScheduledListener(mockServiceScopeFactory.Object);

            var consumeResult = new ConsumeResult<string, PatientCensusScheduled>
            {
                Message = new Message<string, PatientCensusScheduled>
                {
                    Value = new PatientCensusScheduled()
                }
            };

            await listener.ExecuteListenerAsync(consumeResult, default);

            // Add assertions to verify behavior
        }
    }
}
