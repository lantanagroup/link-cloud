using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Listeners;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
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
    public class PatientEventListenerTests
    {
        private AutoMocker _mocker;

        [Fact]
        public void PatientEventListenerTest()
        {
            _mocker = new AutoMocker();
            _mocker.Use(new ServiceDescriptor(typeof(ConsumerConfig), new ConsumerConfig()));

            var _patientEventListener = _mocker.CreateInstance<PatientEventListener>();
            var cancellationToken = new CancellationToken();

            var executeAsyncMethod = typeof(PatientEventListener).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            executeAsyncMethod.Invoke(_patientEventListener, new object[] { cancellationToken });

            _mocker.GetMock<IKafkaConsumerFactory<string, PatientEventValue>>()
                .Verify(x => x.CreateConsumer(It.IsAny<ConsumerConfig>(), null, null), Times.Once);
        }

        [Fact]
        public void PatientEventListenerNegativeTest()
        {
            _mocker = new AutoMocker();
            _mocker.Use(new ServiceDescriptor(typeof(ConsumerConfig), new ConsumerConfig()));

            var _patientEventListener = _mocker.CreateInstance<PatientEventListener>();
            var cancellationToken = new CancellationToken();

            var executeAsyncMethod = typeof(PatientEventListener).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            executeAsyncMethod.Invoke(_patientEventListener, new object[] { cancellationToken });

            _mocker.GetMock<IKafkaConsumerFactory<string, PatientEventValue>>()
                .Verify(x => x.CreateConsumer(null, null, null), Times.Never);
        }
    }
}
