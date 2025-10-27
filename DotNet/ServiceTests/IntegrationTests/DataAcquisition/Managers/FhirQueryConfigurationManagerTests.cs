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

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FhirQueryConfigurationManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
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

    [Fact]
    public async Task CreateAuthenticationConfiguration_Valid_ReturnsModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
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
        var result = await manager.CreateAuthenticationConfiguration("TestFacility", authConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AuthType.Basic.ToString(), result.AuthType);
    }

    [Fact]
    public async Task CreateAuthenticationConfiguration_NoConfig_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var authConfig = new AuthenticationConfiguration
        {
            AuthType = AuthType.Basic.ToString()
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.CreateAuthenticationConfiguration("NonExisting", authConfig));
    }

    [Fact]
    public async Task CreateAuthenticationConfiguration_ExistingAuth_ThrowsAlreadyExists()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
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
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => manager.CreateAuthenticationConfiguration("TestFacility", authConfig));
    }

    [Fact]
    public async Task UpdateAuthenticationConfiguration_Valid_ReturnsModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
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
        var result = await manager.UpdateAuthenticationConfiguration("TestFacility", authConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AuthType.OAuth.ToString(), result.AuthType);
    }

    [Fact]
    public async Task UpdateAuthenticationConfiguration_NoConfig_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var authConfig = new AuthenticationConfiguration
        {
            AuthType = AuthType.Basic.ToString()
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.UpdateAuthenticationConfiguration("NonExisting", authConfig));
    }

    [Fact]
    public async Task DeleteAuthenticationConfiguration_Valid_DeletesAuth()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            Authentication = new AuthenticationConfiguration { AuthType = AuthType.Basic.ToString() }
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        await manager.DeleteAuthenticationConfiguration("TestFacility");

        // Assert
        var updatedConfig = await dbContext.FhirQueryConfigurations.SingleOrDefaultAsync(c => c.FacilityId == "TestFacility");
        Assert.Null(updatedConfig.Authentication);
    }

    [Fact]
    public async Task DeleteAuthenticationConfiguration_NoConfig_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAuthenticationConfiguration("NonExisting"));
    }

    [Fact]
    public async Task CreateAsync_Valid_ReturnsModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var model = new CreateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            TimeZone = "utc",
            Authentication = new AuthenticationConfigurationModel
            {
                Audience = "test",
                AuthType = AuthType.OAuth.ToString(),
                Key = "test",
                TokenUrl = "test",
                ClientId =  "test",
                Password = "test",  
                UserName = "test",
            }
        };

        // Act
        var result = await manager.CreateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestFacility", result.FacilityId);
    }

    [Fact]
    public async Task CreateAsync_MissingFacilityId_ThrowsArgumentNull()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = new CreateFhirQueryConfigurationModel
        {
            FhirServerBaseUrl = "http://example.com"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_MissingFhirServerBaseUrl_ThrowsArgumentNull()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = new CreateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_Valid_ReturnsModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var existing = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://old.com",
        };
        dbContext.FhirQueryConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://new.com"
        };

        // Act
        var result = await manager.UpdateAsync(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("http://new.com", result.FhirServerBaseUrl);
    }

    [Fact]
    public async Task UpdateAsync_NoExisting_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = "NonExisting",
            FhirServerBaseUrl = "http://example.com"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task UpdateAsync_MissingFhirServerBaseUrl_ThrowsArgumentNull()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var model = new UpdateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.UpdateAsync(model));
    }

    [Fact]
    public async Task DeleteAsync_Valid_ReturnsTrue()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.DeleteAsync("TestFacility");

        // Assert
        Assert.True(result);
        var deleted = await dbContext.FhirQueryConfigurations.SingleOrDefaultAsync(c => c.FacilityId == "TestFacility");
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_NoExisting_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAsync("NonExisting"));
    }
}