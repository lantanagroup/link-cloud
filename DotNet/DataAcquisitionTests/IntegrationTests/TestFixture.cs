using DataAcquisition.Domain.Infrastructure.Context;
using DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LantanaGroup.Link.DataAcquisitionTests.IntegrationTests;

[CollectionDefinition("IntegrationTests")]
public class TestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; private set; }

    public TestFixture()
    {
        var services = new ServiceCollection();

        // Configure in-memory database
        services.AddDbContext<DataAcquisitionDbContext>(options =>
            options.UseInMemoryDatabase("TestDatabase"));

        // Register other dependencies (mocked or real)
        // Example: services.AddScoped<IMyService, MyService>();

        ServiceProvider = services.BuildServiceProvider();

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Add test data to the in-memory database
        context.FhirQueryConfigurations.Add(new FhirQueryConfiguration { Id = "1", FacilityId = "TestFacility" });
        context.SaveChanges();
    }

    public void Dispose()
    {
        // Clean up resources
    }
}
