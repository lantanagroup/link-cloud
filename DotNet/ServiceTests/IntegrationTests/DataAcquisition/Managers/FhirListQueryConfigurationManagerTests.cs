using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FhirListQueryConfigurationManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private IFhirListQueryConfigurationManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new FhirListQueryConfigurationManager(database);
    }

    [Fact]
    public async Task CreateAsync_FacilityHasSftpConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        const string facilityId = "TestFacility";

        // Create existing SFTP configuration for the facility
        var sftpConfig = new SftpConfiguration
        {
            Id = Guid.NewGuid(),
            OrganizationId = facilityId,
            Host = "sftp.example.com",
            Port = 22,
            RemoteDirectory = "/data",
            Timeout = TimeSpan.FromSeconds(60)
        };

        dbContext.SftpConfigurations.Add(sftpConfig);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var fhirListConfigModel = new CreateFhirListConfigurationModel
        {
            FacilityId = facilityId,
            FhirBaseServerUrl = "https://fhir.example.com",
            EHRPatientLists = new List<EhrPatientListModel>()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.CreateAsync(fhirListConfigModel, CancellationToken.None));

        Assert.Contains(facilityId, exception.Message);
    }
}
