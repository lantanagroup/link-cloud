using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;

namespace LantanaGroup.Link.Tenant.Business.Managers
{
    public interface IVendorManager
    {
        Task<VendorModel> CreateVendorAsync(VendorModel newVendor, CancellationToken cancellationToken = default);
        Task<VendorVersionModel> CreateVendorVersionAsync(VendorVersionModel newVendorVersion, CancellationToken cancellationToken = default);
        Task<VendorModel> UpdateVendorAsync(Guid id, VendorModel vendor, CancellationToken cancellationToken = default);
        Task<VendorVersionModel> UpdateVendorVersionAsync(Guid id, VendorVersionModel vendorVersion, CancellationToken cancellationToken = default);
        Task DeleteVendorAsync(Guid vendorId, CancellationToken cancellationToken = default);
        Task DeleteVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default);
    }

    public class VendorManager : IVendorManager
    {
        private readonly ILogger<VendorManager> _logger;
        private readonly TenantDbContext _dbContext;

        public VendorManager(
            ILogger<VendorManager> logger,
            TenantDbContext dbContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<VendorModel> CreateVendorAsync(VendorModel newVendor, CancellationToken cancellationToken = default)
        {
            if(newVendor == null)
            {
                throw new ArgumentNullException(nameof(newVendor));
            }
            if (string.IsNullOrEmpty(newVendor.Name))
            {
                throw new ArgumentNullException(nameof(newVendor.Name));
            }

            var existingVendor = await _dbContext.Vendors.FirstOrDefaultAsync(v => v.Name == newVendor.Name, cancellationToken);
            if (existingVendor != null)
            {
                throw new InvalidOperationException($"Vendor with name '{newVendor.Name}' already exists.");
            }

            var vendorEntity = new Vendor
            {
                Id = Guid.NewGuid(),
                Name = newVendor.Name
            };
            var vendorVersionEntity = new VendorVersion
            {
                Id = Guid.NewGuid(),
                VendorId = vendorEntity.Id,
                Version = "default"
            };
            await _dbContext.Vendors.AddAsync(vendorEntity, cancellationToken);
            await _dbContext.VendorVersions.AddAsync(vendorVersionEntity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new VendorModel
            {
                Id = vendorEntity.Id,
                Name = vendorEntity.Name
            };
        }

        public async Task<VendorVersionModel> CreateVendorVersionAsync(VendorVersionModel newVendorVersion, CancellationToken cancellationToken = default)
        {
            if(newVendorVersion == null)
            {
                throw new ArgumentNullException(nameof(newVendorVersion));
            }
            if (string.IsNullOrEmpty(newVendorVersion.Version))
            {
                throw new ArgumentNullException(nameof(newVendorVersion.Version));
            }

            var existingVendor = await _dbContext.Vendors.FirstOrDefaultAsync(v => v.Id == newVendorVersion.VendorId, cancellationToken);
            if (existingVendor == null)
            {
                throw new InvalidOperationException($"Vendor with ID '{newVendorVersion.VendorId}' does not exist.");
            }

            var existingVendorVersion = await _dbContext.VendorVersions.FirstOrDefaultAsync(vv => vv.VendorId == newVendorVersion.VendorId && vv.Version == newVendorVersion.Version, cancellationToken);
            if (existingVendorVersion != null)
            {
                throw new InvalidOperationException($"Vendor version '{newVendorVersion.Version}' for vendor ID '{newVendorVersion.VendorId}' already exists.");
            }

            var vendorVersionEntity = new VendorVersion
            {
                Id = Guid.NewGuid(),
                VendorId = newVendorVersion.VendorId!.Value,
                Version = newVendorVersion.Version
            };
            await _dbContext.VendorVersions.AddAsync(vendorVersionEntity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new VendorVersionModel
            {
                Id = vendorVersionEntity.Id,
                VendorId = vendorVersionEntity.VendorId,
                Version = vendorVersionEntity.Version
            };
        }

        public async Task DeleteVendorAsync(Guid vendorId, CancellationToken cancellationToken = default)
        {
            await _dbContext.Vendors.Where(q => q.Id == vendorId).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task DeleteVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default)
        {
            await _dbContext.VendorVersions.Where(q => q.Id == vendorVersionId).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<VendorModel> UpdateVendorAsync(Guid id, VendorModel vendor, CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(vendor.Name))
            {
                throw new ArgumentNullException(nameof(vendor.Name));
            }

            var existingVendor = await _dbContext.Vendors.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (existingVendor == null)
            {
                throw new InvalidOperationException($"Vendor with ID '{id}' does not exist.");
            }
            
            existingVendor.Name = vendor.Name ?? existingVendor.Name;

            _dbContext.Vendors.Update(existingVendor);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new VendorModel
            {
                Id = existingVendor.Id,
                Name = existingVendor.Name
            };
        }

        public async Task<VendorVersionModel> UpdateVendorVersionAsync(Guid id, VendorVersionModel vendorVersion, CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(vendorVersion.Version))
            {
                throw new ArgumentNullException(nameof(vendorVersion.Version));
            }

            var existingVendorVersion = await _dbContext.VendorVersions.FirstOrDefaultAsync(vv => vv.Id == id, cancellationToken);
            if (existingVendorVersion == null)
            {
                throw new InvalidOperationException($"Vendor version with ID '{id}' does not exist.");
            }

            existingVendorVersion.Version = vendorVersion.Version;

            _dbContext.VendorVersions.Update(existingVendorVersion);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new VendorVersionModel
            {
                Id = existingVendorVersion.Id,
                VendorId = existingVendorVersion.VendorId,
                Version = existingVendorVersion.Version
            };
        }
    }
}