using Amazon.Runtime.Internal.Util;
using Confluent.Kafka;
using Grpc.Core;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Entities;
using LantanaGroup.Link.DataAcquisition.Listeners;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Attributes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using MongoDB.Driver;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests
{
    public class QueryListenerTests
    {
        private AutoMocker? _mocker;

        // Was told to hold off on this until standardization issues are resolved
        [Fact]
        public async Task StartConsumerLoopTest()
        {
            /*_mocker = new AutoMocker();
            var _cancellatioTokenSource = new CancellationTokenSource();
            var _logger = _mocker.GetMock<ILogger<DataAcqTenantConfigMongoRepo>>();
            var _options = _mocker.GetMock<IOptions<MongoConnection>>();
            var _dataBaseMock = _mocker.GetMock<IMongoDatabase>();
            var _collection = _mocker.GetMock<IMongoCollection<TenantDataAcquisitionConfigModel>>();

            //var _tenantConfigRepo = new Mock<DataAcqTenantConfigMongoRepo>(_options.Object, _logger.Object);

            //_options.Setup(o => o.Value.ConnectionString).Returns("testConnection");
           // _options.Setup(o => o.Value.DatabaseName).Returns("testDatabase");
            _dataBaseMock.Setup(d => d.GetCollection<TenantDataAcquisitionConfigModel>(It.IsAny<string>(), null)).Returns(_collection.Object);

            var _tenantConfigRepo = new DataAcqTenantConfigMongoRepo(
                _options.Object
                );

             //var _dataBaseField = typeof(DataAcqTenantConfigMongoRepo).GetField("_database", BindingFlags.NonPublic | BindingFlags.Instance);
             //_dataBaseField.SetValue(_tenantConfigRepo.Object, _dataBaseMock.Object);
             //_dataBaseMock.Setup(g => g.GetCollection<TenantDataAcquisitionConfigModel>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
             //    .Returns(_collection.Object);

            var _consumer = _mocker.GetMock<IConsumer<string, string>>();
            var _consumerFactory = _mocker.GetMock<IKafkaConsumerFactory<string, string>>();
            _consumerFactory.Setup(f => f.CreateConsumer(It.IsAny<ConsumerConfig>(), It.IsAny<IDeserializer<string>>(), It.IsAny<IDeserializer<string>>()))
                .Returns(_consumer.Object);

            var _queryListener = new QueryListener(
                new Mock<ILogger<QueryListener>>().Object,
                _tenantConfigRepo,
                _mocker.Get<IMediator>(),
                _consumerFactory.Object,
                _mocker.Get<IKafkaProducerFactory<string, object>>());

            await _queryListener.StartAsync(_cancellatioTokenSource.Token);
            _consumer.Verify(c => c.Consume(It.IsAny<CancellationToken>()), Times.AtLeastOnce);*/
        }

    }
}
