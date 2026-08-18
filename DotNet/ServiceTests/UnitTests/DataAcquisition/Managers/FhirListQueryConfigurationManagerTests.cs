using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Moq;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Managers;

[Trait("Category", "UnitTests")]
public class FhirListQueryConfigurationManagerTests
{
    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<IEntityRepository<FhirListConfiguration>> _listRepo = new();
    private readonly Mock<IEntityRepository<SftpConfiguration>> _sftpRepo = new();

    public FhirListQueryConfigurationManagerTests()
    {
        _database.SetupGet(d => d.FhirListConfigurationRepository).Returns(_listRepo.Object);
        _database.SetupGet(d => d.SftpConfigurationRepository).Returns(_sftpRepo.Object);
    }

    private FhirListQueryConfigurationManager CreateManager() => new(_database.Object);

    [Fact]
    public async Task CreateAuthenticationConfiguration_NoConfig_ThrowsMissingFacilityConfiguration()
    {
        _listRepo.Setup(r => r.SingleOrDefaultAsync(It.IsAny<Expression<Func<FhirListConfiguration, bool>>>()))
            .ReturnsAsync((FhirListConfiguration?)null);

        var manager = CreateManager();

        await Assert.ThrowsAsync<MissingFacilityConfigurationException>(
            () => manager.CreateAuthenticationConfiguration("NonExisting", new AuthenticationConfiguration { AuthType = "Basic" }));
    }

    [Fact]
    public async Task UpdateAuthenticationConfiguration_NoConfig_ThrowsMissingFacilityConfiguration()
    {
        _listRepo.Setup(r => r.SingleOrDefaultAsync(It.IsAny<Expression<Func<FhirListConfiguration, bool>>>()))
            .ReturnsAsync((FhirListConfiguration?)null);

        var manager = CreateManager();

        await Assert.ThrowsAsync<MissingFacilityConfigurationException>(
            () => manager.UpdateAuthenticationConfiguration("NonExisting", new AuthenticationConfiguration { AuthType = "Basic" }));
    }

    [Fact]
    public async Task DeleteAuthenticationConfiguration_NoConfig_ThrowsNotFound()
    {
        _listRepo.Setup(r => r.SingleOrDefaultAsync(It.IsAny<Expression<Func<FhirListConfiguration, bool>>>()))
            .ReturnsAsync((FhirListConfiguration?)null);

        var manager = CreateManager();

        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAuthenticationConfiguration("NonExisting"));
    }

    [Fact]
    public async Task CreateAsync_FacilityHasSftpConfiguration_ThrowsInvalidOperationException()
    {
        var facilityId = "TestFacility";

        _listRepo.Setup(r => r.SingleOrDefaultAsync(
                It.IsAny<Expression<Func<FhirListConfiguration, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirListConfiguration?)null);

        _sftpRepo.Setup(r => r.SingleOrDefaultAsync(
                It.IsAny<Expression<Func<SftpConfiguration, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SftpConfiguration
            {
                Id = Guid.NewGuid(),
                OrganizationId = facilityId,
                Host = "sftp.example.com",
                Port = 22,
                RemoteDirectory = "/data",
                Timeout = TimeSpan.FromSeconds(60)
            });

        var manager = CreateManager();
        var model = new CreateFhirListConfigurationModel
        {
            FacilityId = facilityId,
            FhirBaseServerUrl = "https://fhir.example.com",
            EHRPatientLists = new List<EhrPatientListModel>()
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.CreateAsync(model, CancellationToken.None));

        Assert.Contains(facilityId, ex.Message);
    }
}
