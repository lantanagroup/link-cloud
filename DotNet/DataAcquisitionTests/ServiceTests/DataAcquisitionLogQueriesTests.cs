using Moq;
using Xunit;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

namespace LantanaGroup.Link.DataAcquisitionTests.ServiceTests
{
    public class DataAcquisitionLogQueriesTests
    {
        [Fact]
        public async Task CheckIfReferenceResourceHasBeenSent_ResourceAlreadySent_ReturnsTrueAndSkipsReprocessing()
        {
            var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
            mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref1", "report1", "fac1", "corr1", CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task CheckIfReferenceResourceHasBeenSent_ResourceNotSent_ReturnsFalseAndProceeds()
        {
            var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
            mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref2", "report2", "fac2", "corr2", CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task CheckIfReferenceResourceHasBeenSent_CancellationTokenTriggered_ThrowsOperationCanceledException()
        {
            var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref3", "report3", "fac3", "corr3", cts.Token));
        }

        [Fact]
        public async Task CheckIfReferenceResourceHasBeenSent_UnderlyingQueryFailure_ThrowsException()
        {
            var mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
            mockLogQueries.Setup(q => q.CheckIfReferenceResourceHasBeenSent(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("DB failure"));

            await Assert.ThrowsAsync<System.Exception>(() =>
                mockLogQueries.Object.CheckIfReferenceResourceHasBeenSent("ref4", "report4", "fac4", "corr4", CancellationToken.None));
        }
    }
}
