using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.Queries;
using LantanaGroup.Link.QueryDispatch.Listeners;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QueryDispatchUnitTests
{
    public class ReportScheduledEventListenerTests
    {
        private AutoMocker _mocker;

        [Fact]
        public void ReportScheduledEventListenerTest()
        {
            _mocker = new AutoMocker();
            var _scheduledEventListener = _mocker.CreateInstance<ReportScheduledEventListener>();
            var cancellationToken = new CancellationToken();

            _mocker.GetMock<IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue>>().Setup(x =>
            x.CreateConsumer(It.IsAny<ConsumerConfig>(), null, null))
                .Returns(_mocker.Get<IConsumer<ReportScheduledKey, ReportScheduledValue>>());

            var executeAsyncMethod = typeof(ReportScheduledEventListener).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            executeAsyncMethod.Invoke(_scheduledEventListener, new object[] { cancellationToken });

            _mocker.Verify<IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue>>(factory => 
            factory.CreateConsumer(It.IsAny<ConsumerConfig>(), null, null), Times.Once);
        }

        [Fact]
        public void ReportScheduledEventListenerNegativeTest()
        {
            _mocker = new AutoMocker();
            _mocker.Use(new ServiceDescriptor(typeof(ConsumerConfig), new ConsumerConfig()));

            var _scheduledEventListener = _mocker.CreateInstance<ReportScheduledEventListener>();
            var cancellationToken = new CancellationToken();

            var executeAsyncMethod = typeof(ReportScheduledEventListener).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            executeAsyncMethod.Invoke(_scheduledEventListener, new object[] { cancellationToken });

            _mocker.GetMock<IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue>>()
                .Verify(x => x.CreateConsumer(null, null, null), Times.Never);
        }
    }
}
