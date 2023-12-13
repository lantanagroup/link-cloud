using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.MeasureEval.Models;

namespace LantanaGroup.Link.MeasureEval.Listeners.Tests
{
    /**
     * See https://www.baeldung.com/kafka-mockconsumer for guidance on mocking kafka consumers
     */
    [TestClass()]
    public class PatientDataNormalizedListenerTests
    {
        /**
         * Verifies that the Kafka message produced by the listener has the expected information
         */
        private bool VerifyProducedMessage(Message<Null, PatientDataEvaluatedMessage> message)
        {
            return message.Value != null &&
                   message.Value.MeasureId == "TestMeasureId" &&
                   message.Value.TenantId == "TestTenantId" && 
                   message.Value.PatientId == "TestPatientId" &&
                   message.Value.Result != null;
        }

        /**
         * Verifies that the HttpClient POST that is produced by the listener has the expected information
         */
        private bool VerifyPost(HttpRequestMessage message)
        {
            if (message == null || message.Content == null) return false;
            if (message.Content is not StringContent) return false;

            StringContent stringContent = (StringContent) message.Content;
            string stringContentValue = stringContent.ReadAsStringAsync().Result;
            Parameters parameters = new FhirJsonParser().Parse<Parameters>(stringContentValue);

            return message.RequestUri != null &&
                   message.RequestUri.AbsoluteUri == "http://test.com/fhir/Measure/TestMeasureId/$evaluate-measure" &&
                   parameters != null &&
                   parameters.Parameter.Count == 4;
        }

        [TestMethod()]
        public async Task PatientDataNormalizedListenerTest()
        {
            //Mock<ILogger<PatientDataNormalizedListener>> mockLogger = new Mock<ILogger<PatientDataNormalizedListener>>(); ;
            //Mock<IConsumer<Ignore, PatientDataNormalizedMessage>> mockConsumer =
            //    new Mock<IConsumer<Ignore, PatientDataNormalizedMessage>>();
            //Mock<IProducer<Null, PatientDataEvaluatedMessage>> mockProducer =
            //    new Mock<IProducer<Null, PatientDataEvaluatedMessage>>();
            //Mock<HttpMessageHandler> mockClientHandler = new Mock<HttpMessageHandler>();

            //// Construct the listener and override the HttpClient's message handler
            //PatientDataNormalizedListener listener = new PatientDataNormalizedListener(mockLogger.Object, mockConsumer.Object, mockProducer.Object, "http://test.com/fhir");
            //listener._httpMessageHandler = mockClientHandler.Object;

            //// Mimic producing a message to be consumed
            //mockConsumer.Setup(m => m.Consume(It.IsAny<CancellationToken>()))
            //    .Returns(() =>
            //    {
            //        return new ConsumeResult<Ignore, PatientDataNormalizedMessage>()
            //        {
            //            Message = new Message<Ignore, PatientDataNormalizedMessage>()
            //            {
            //                Value = new PatientDataNormalizedMessage()
            //                {
            //                    TenantId = "TestTenantId",
            //                    MeasureId = "TestMeasureId",
            //                    PatientId = "TestPatientId",
            //                    PeriodStart = new DateTime(2021, 6, 1),
            //                    PeriodEnd = new DateTime(2021, 6, 2),
            //                    Data = new Bundle()
            //                    {
            //                        Id = "TestBundleId"
            //                    }
            //                }
            //            }
            //        };
            //    });

            //// Mimic the CQF-Ruler responding with a MeasureReport
            //mockClientHandler
            //    .Protected()
            //    .Setup<Task<HttpResponseMessage>>("SendAsync",
            //        ItExpr.Is<HttpRequestMessage>(m => VerifyPost(m)),
            //        ItExpr.IsAny<CancellationToken>())
            //    .ReturnsAsync(() =>
            //    {
            //        MeasureReport mr = new MeasureReport();
            //        HttpResponseMessage message = new HttpResponseMessage();
            //        message.StatusCode = HttpStatusCode.OK;
            //        message.Content = new StringContent(new FhirJsonSerializer().SerializeToString(mr));
            //        return message;
            //    });

            //// Expect the listener to produce a message
            //mockProducer
            //    .Setup(m => m.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<Null, PatientDataEvaluatedMessage>>(),
            //        It.IsAny<CancellationToken>()))
            //    .Callback(() =>
            //    {
            //        // Stop the listener from polling after the first message has been produced
            //        listener.StopAsync(CancellationToken.None);
            //    })
            //    .ReturnsAsync(() => new DeliveryResult<Null, PatientDataEvaluatedMessage>());

            //// Start the listener
            //await listener.StartAsync(CancellationToken.None);

            //// Verify that the listener produced a single message and flushed it
            //mockProducer.Verify(
            //    p =>
            //        p.ProduceAsync("PatientDataEvaluated", It.Is<Message<Null, PatientDataEvaluatedMessage>>(m => VerifyProducedMessage(m)), It.IsAny<CancellationToken>()),
            //    Times.Once);
            //mockProducer.Verify(
            //    p => p.Flush(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}