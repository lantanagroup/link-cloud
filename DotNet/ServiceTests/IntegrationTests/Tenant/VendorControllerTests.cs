using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Controllers;
using LantanaGroup.Link.Tenant.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class VendorControllerTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IVendorManager _vendorManager;
    private readonly VendorController _controller;

    public VendorControllerTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var serviceProvider = _scope.ServiceProvider;

        _vendorManager = serviceProvider.GetRequiredService<IVendorManager>();
        _controller = new VendorController(
            serviceProvider.GetRequiredService<ILogger<VendorController>>(),
            _vendorManager,
            serviceProvider.GetRequiredService<IVendorQueries>());
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task Post_DuplicateVendor_ReturnsConflict()
    {
        var vendorName = $"Vendor-{Guid.NewGuid():N}";
        await _vendorManager.CreateVendorAsync(new VendorModel { Name = vendorName });

        var result = await _controller.Post(new CreateVendorModel { Name = vendorName });

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }
}