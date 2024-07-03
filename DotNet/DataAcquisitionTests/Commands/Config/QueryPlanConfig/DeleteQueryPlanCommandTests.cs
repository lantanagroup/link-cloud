using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using MediatR;
using Moq;
using Moq.AutoMock;

namespace DataAcquisitionUnitTests.Commands.Config.QueryPlanConfig
{
    public class DeleteQueryPlanCommandTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<DeleteQueryPlanCommandHandler>();
            var command = new DeleteQueryPlanCommand
            {
                FacilityId = facilityId
            };

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Unit.Value));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Verify(r => r.DeleteAsync(command.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
        }

        [Fact]
        public async Task HandleNegativeTest_NoFacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<DeleteQueryPlanCommandHandler>();
            var command = new DeleteQueryPlanCommand
            {
                FacilityId = "InvalidId"
            };

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Invalid FacilityId"));

            await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Verify(r => r.DeleteAsync(command.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
