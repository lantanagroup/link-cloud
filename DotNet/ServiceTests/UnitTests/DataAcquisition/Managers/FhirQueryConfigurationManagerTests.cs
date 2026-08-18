using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
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
public class FhirQueryConfigurationManagerTests
{
    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<IEntityRepository<FhirQueryConfiguration>> _repo = new();

    public FhirQueryConfigurationManagerTests()
    {
        _database.SetupGet(d => d.FhirQueryConfigurationRepository).Returns(_repo.Object);
    }

    private FhirQueryConfigurationManager CreateManager() => new(_database.Object);

    private void SetupFirstOrDefault(FhirQueryConfiguration? entity)
    {
        _repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<FhirQueryConfiguration, bool>>>()))
            .ReturnsAsync(entity);
    }

    private void SetupSingleOrDefault(FhirQueryConfiguration? entity)
    {
        _repo.Setup(r => r.SingleOrDefaultAsync(It.IsAny<Expression<Func<FhirQueryConfiguration, bool>>>()))
            .ReturnsAsync(entity);
    }

    [Fact]
    public async Task CreateAuthenticationConfiguration_NoConfig_ThrowsNotFound()
    {
        SetupFirstOrDefault(null);
        var manager = CreateManager();

        await Assert.ThrowsAsync<NotFoundException>(
            () => manager.CreateAuthenticationConfiguration("NonExisting", new AuthenticationConfiguration { AuthType = AuthType.Basic.ToString() }));
    }

    [Fact]
    public async Task UpdateAuthenticationConfiguration_NoConfig_ThrowsNotFound()
    {
        SetupFirstOrDefault(null);
        var manager = CreateManager();

        await Assert.ThrowsAsync<NotFoundException>(
            () => manager.UpdateAuthenticationConfiguration("NonExisting", new AuthenticationConfiguration { AuthType = AuthType.Basic.ToString() }));
    }

    [Fact]
    public async Task DeleteAuthenticationConfiguration_NoConfig_ThrowsNotFound()
    {
        SetupFirstOrDefault(null);
        var manager = CreateManager();

        await Assert.ThrowsAsync<NotFoundException>(
            () => manager.DeleteAuthenticationConfiguration("NonExisting"));
    }

    [Fact]
    public async Task CreateAsync_MissingFacilityId_ThrowsArgumentNull()
    {
        var manager = CreateManager();
        var model = new CreateFhirQueryConfigurationModel
        {
            FhirServerBaseUrl = "http://example.com"
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_MissingFhirServerBaseUrl_ThrowsArgumentNull()
    {
        var manager = CreateManager();
        var model = new CreateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility"
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(model));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task CreateAsync_MaxRetries_Invalid_ThrowsArgumentOutOfRange(int maxRetries)
    {
        var manager = CreateManager();
        var model = new CreateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MaxRetries = maxRetries
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_NoExisting_ThrowsNotFound()
    {
        SetupSingleOrDefault(null);
        var manager = CreateManager();
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = "NonExisting",
            FhirServerBaseUrl = "http://example.com"
        };

        await Assert.ThrowsAsync<NotFoundException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_MissingFhirServerBaseUrl_ThrowsArgumentNull()
    {
        var manager = CreateManager();
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility"
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.UpdateAsync(model));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task UpdateAsync_MaxRetries_Invalid_ThrowsArgumentOutOfRange(int maxRetries)
    {
        var manager = CreateManager();
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MaxRetries = maxRetries
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task DeleteAsync_NoExisting_ThrowsNotFound()
    {
        SetupFirstOrDefault(null);
        var manager = CreateManager();

        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAsync("NonExisting"));
    }
}
