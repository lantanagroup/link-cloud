using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FhirQueryConfigurationManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public FhirQueryConfigurationManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IFhirQueryConfigurationManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new FhirQueryConfigurationManager(database);
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAuthenticationConfiguration_Valid_ReturnsModel()
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com"
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var authConfig = new AuthenticationConfiguration
        {
            AuthType = AuthType.Basic.ToString()
            // Add other properties as needed
        };

        // Act
        var result = await manager.CreateAuthenticationConfiguration(facilityId, authConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AuthType.Basic.ToString(), result.AuthType);
    }

    [Fact]
    public async Task CreateAuthenticationConfiguration_ExistingAuth_ThrowsAlreadyExists()
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            Authentication = new AuthenticationConfiguration { AuthType = AuthType.Basic.ToString() }
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var authConfig = new AuthenticationConfiguration
        {
            AuthType = AuthType.Basic.ToString()
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => manager.CreateAuthenticationConfiguration(facilityId, authConfig));
    }

    [Fact]
    public async Task UpdateAuthenticationConfiguration_Valid_ReturnsModel()
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            Authentication = new AuthenticationConfiguration { AuthType = AuthType.Basic.ToString() }
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var authConfig = new AuthenticationConfiguration
        {
            AuthType = AuthType.OAuth.ToString()
        };

        // Act
        var result = await manager.UpdateAuthenticationConfiguration(facilityId, authConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AuthType.OAuth.ToString(), result.AuthType);
    }

    [Fact]
    public async Task DeleteAuthenticationConfiguration_Valid_DeletesAuth()
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            Authentication = new AuthenticationConfiguration { AuthType = AuthType.Basic.ToString() }
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        await manager.DeleteAuthenticationConfiguration(facilityId);

        // Assert
        var updatedConfig = await dbContext.FhirQueryConfigurations.SingleOrDefaultAsync(c => c.FacilityId == facilityId);
        Assert.Null(updatedConfig.Authentication);
    }

    [Fact]
    public async Task CreateAsync_Valid_ReturnsModel()
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var manager = CreateManager(scope);
        var model = new CreateFhirQueryConfigurationModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            TimeZone = "utc",
            Authentication = new AuthenticationConfigurationModel
            {
                Audience = "test",
                AuthType = AuthType.OAuth.ToString(),
                Key = "test",
                TokenUrl = "test",
                ClientId = "test",
                Password = "test",
                UserName = "test",
            }
        };

        // Act
        var result = await manager.CreateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
    }

    [Fact]
    public async Task UpdateAsync_Valid_ReturnsModel()
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var existing = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://old.com",
        };
        dbContext.FhirQueryConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://new.com"
        };

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("http://new.com", result.FhirServerBaseUrl);
    }

    [Fact]
    public async Task DeleteAsync_Valid_ReturnsTrue()
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com"
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.DeleteAsync(facilityId);

        // Assert
        Assert.True(result);
        var deleted = await dbContext.FhirQueryConfigurations.SingleOrDefaultAsync(c => c.FacilityId == facilityId);
        Assert.Null(deleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(10)]
    public async Task CreateAsync_MaxRetries_Valid_ReturnsModel(int? maxRetries)
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var manager = CreateManager(scope);
        var model = new CreateFhirQueryConfigurationModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MaxRetries = maxRetries
        };

        // Act
        var result = await manager.CreateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(maxRetries, result.MaxRetries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(10)]
    public async Task UpdateAsync_MaxRetries_Valid_ReturnsModel(int? maxRetries)
    {
        // Arrange
        var facilityId = NewFacilityId("TestFacility");
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var existing = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://old.com",
        };
        dbContext.FhirQueryConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://new.com",
            MaxRetries = maxRetries
        };

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(maxRetries, result.MaxRetries);
    }

    }

