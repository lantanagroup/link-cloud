using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class SftpConfigurationManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private ISftpConfigurationManager CreateManager(IServiceScope scope)
    {
        var logger = new Mock<ILogger<SftpConfigurationManager>>().Object;
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var fhirListConfigurationQueries = scope.ServiceProvider.GetRequiredService<IFhirQueryListConfigurationQueries>();
        return new SftpConfigurationManager(logger, database, fhirListConfigurationQueries);
    }

    [Fact]
    public async Task CreateAsync_FacilityHasFhirListConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        const string organizationId = "TestFacility";

        // Create existing FHIR List configuration for the facility
        var fhirListConfig = new FhirListConfiguration
        {
            Id = Guid.NewGuid(),
            FacilityId = organizationId,
            FhirBaseServerUrl = "https://fhir.example.com",
            EHRPatientLists = new List<EhrPatientList>()
        };

        dbContext.FhirListConfigurations.Add(fhirListConfig);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var sftpConfigModel = new SftpConfigurationModel
        {
            Host = "sftp.example.com",
            Port = 22,
            RemoteDirectory = "/data",
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.CreateAsync(sftpConfigModel, organizationId, CancellationToken.None));

        Assert.Contains(organizationId, exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_OrganizationIdMismatch_ThrowsOrganizationalAccessException()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        const string storedOrganizationId = "StoredOrganization";
        const string requestOrganizationId = "DifferentOrganization";

        // Create existing SFTP configuration for a different organization
        var existingConfig = new SftpConfiguration
        {
            Id = Guid.NewGuid(),
            OrganizationId = storedOrganizationId,
            Host = "sftp.example.com",
            Port = 22,
            RemoteDirectory = "/data",
            Timeout = TimeSpan.FromSeconds(60)
        };

        dbContext.SftpConfigurations.Add(existingConfig);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var updateModel = new SftpConfigurationModel
        {
            Id = existingConfig.Id,
            Host = "sftp.updated.com",
            Port = 22,
            RemoteDirectory = "/updated-data",
            Timeout = TimeSpan.FromSeconds(120)
        };

        // Act & Assert
        await Assert.ThrowsAsync<OrganizationalAccessException>(
            () => manager.UpdateAsync(requestOrganizationId, updateModel, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_OrganizationIdMismatch_ThrowsOrganizationalAccessException()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        const string storedOrganizationId = "StoredOrganization";
        const string requestOrganizationId = "DifferentOrganization";

        // Create existing SFTP configuration for a different organization
        var existingConfig = new SftpConfiguration
        {
            Id = Guid.NewGuid(),
            OrganizationId = storedOrganizationId,
            Host = "sftp.example.com",
            Port = 22,
            RemoteDirectory = "/data",
            Timeout = TimeSpan.FromSeconds(60)
        };
        dbContext.SftpConfigurations.Add(existingConfig);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<OrganizationalAccessException>(
            () => manager.DeleteAsync(requestOrganizationId, existingConfig.Id, CancellationToken.None));
    }
}
