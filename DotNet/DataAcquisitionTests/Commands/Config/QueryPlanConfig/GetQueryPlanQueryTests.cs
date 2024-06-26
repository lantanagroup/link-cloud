﻿using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

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
