using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisitionTests.IntegrationTests
{
    public class ReadyToAcquireListenerTests : IClassFixture<TestFixture>
    {
        private readonly ReadyToAcquireListener _listener;

        public ReadyToAcquireListenerTests(TestFixture fixture)
        {
            var serviceScopeFactory = fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            _listener = new ReadyToAcquireListener(serviceScopeFactory);
        }

        [Fact]
        public async Task ExecuteListenerAsync_ShouldProcessMessage()
        {
            // Arrange
            var consumeResult = new ConsumeResult<Null, ReadyToAcquire>
            {
                Message = new Message<Null, ReadyToAcquire>
                {
                    Value = new ReadyToAcquire { FacilityId = "TestFacility" }
                }
            };

            // Act
            await _listener.ExecuteListenerAsync(consumeResult, default);

            // Assert
            // Verify expected behavior (e.g., database updates, logs, etc.)
        }
    }
}
