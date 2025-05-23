using DataAcquisition.Domain.Application.Services;
using Moq;
using Xunit;

namespace LantanaGroup.Link.DataAcquisitionTests.Integration
{
    public class PatientDataServiceTests
    {
        [Fact]
        public async Task ValidateFacilityConnection_ShouldValidateSuccessfully()
        {
            var mockDatabase = new Mock<IDatabase>();
            var service = new PatientDataService(mockDatabase.Object, /* other dependencies */);

            var request = new GetPatientDataRequest();
            var result = await service.ValidateFacilityConnection(request, default);

            Assert.NotNull(result);
        }
    }
}
