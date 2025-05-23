using DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LantanaGroup.Link.DataAcquisitionTests.IntegrationTests;

[Collection("IntegrationTests")]
public class DataAcquisitionDbContextTests : IClassFixture<TestFixture>
{
    private readonly DataAcquisitionDbContext _context;

    public DataAcquisitionDbContextTests(TestFixture fixture)
    {
        _context = fixture.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
    }

    [Fact]
    public void CanCreateDbContext()
    {
        Assert.NotNull(_context);
        Assert.NotNull(_context.FhirQueryConfigurations);
        Assert.NotNull(_context.DataAcquisitionLogs);
    }

    [Fact]
    public void CanSeedTestData()
    {
        var config = _context.FhirQueryConfigurations.FirstOrDefault();
        Assert.NotNull(config);
        Assert.Equal("TestFacility", config.FacilityId);
    }
}
