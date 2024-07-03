using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Moq;
using Moq.AutoMock;

namespace DataAcquisitionUnitTests.Commands.Config.QueryPlanConfig
{
    public class GetQueryPlanQueryTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetQueryPlanQueryHandler>();
            var query = new GetQueryPlanQuery
            {
                FacilityId = facilityId
            };

            var expectedResult = new QueryPlan();

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(expectedResult);

            var result = await handler.Handle(query, CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task HandleNegativeTest_Without_FacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetQueryPlanQueryHandler>();
            var query = new GetQueryPlanQuery
            {
                FacilityId = null
            };

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync((QueryPlan?)null);

            var result = await handler.Handle(query, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
