using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
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

    /// <summary>
    /// A vendor version cannot be deleted while something still points at it. The controllers answer
    /// this with 409 Conflict; the message names which reference stood in the way.
    /// </summary>
    public sealed class VendorVersionInUseException : InvalidOperationException
    {
        public VendorVersionInUseException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    public class VendorManager : IVendorManager
    {
        private readonly ILogger<VendorManager> _logger;
        private readonly TenantDbContext _dbContext;
        private readonly INormalizationServiceClient _normalizationServiceClient;

        public VendorManager(
            ILogger<VendorManager> logger,
            TenantDbContext dbContext,
            INormalizationServiceClient normalizationServiceClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _normalizationServiceClient = normalizationServiceClient ?? throw new ArgumentNullException(nameof(normalizationServiceClient));
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
                Name = newVendor.Name,
                Authentication = newVendor.Authentication
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
                Name = vendorEntity.Name,
                Authentication = vendorEntity.Authentication
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
            var vendorVersionIds = await _dbContext.VendorVersions
                .Where(vendorVersion => vendorVersion.VendorId == vendorId)
                .Select(vendorVersion => vendorVersion.Id)
                .ToListAsync(cancellationToken);

            foreach (var vendorVersionId in vendorVersionIds)
            {
                await EnsureVendorVersionIsNotInUseAsync(vendorVersionId, cancellationToken);
            }

            //deleting the vendor cascades to its versions, so a facility still holding one of them
            //fails the very foreign key the version delete would.
            await DeleteAndTranslateStillReferencedAsync(
                () => _dbContext.Vendors.Where(q => q.Id == vendorId).ExecuteDeleteAsync(cancellationToken),
                $"Vendor '{vendorId}' has a version that is still referenced and cannot be deleted.");
        }

        public async Task DeleteVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default)
        {
            await EnsureVendorVersionIsNotInUseAsync(vendorVersionId, cancellationToken);

            await DeleteAndTranslateStillReferencedAsync(
                () => _dbContext.VendorVersions.Where(q => q.Id == vendorVersionId).ExecuteDeleteAsync(cancellationToken),
                $"Vendor version '{vendorVersionId}' is still referenced and cannot be deleted.");
        }

        private async Task EnsureVendorVersionIsNotInUseAsync(Guid vendorVersionId, CancellationToken cancellationToken)
        {
            //the local reference comes first: one indexed read against the service's own database,
            //before an HTTP call to Normalization that a facility-held version makes pointless.
            await EnsureVendorVersionIsNotAssignedToAFacilityAsync(vendorVersionId, cancellationToken);
            await EnsureVendorVersionIsNotReferencedByNormalizationAsync(vendorVersionId, cancellationToken);
        }

        private async Task EnsureVendorVersionIsNotAssignedToAFacilityAsync(Guid vendorVersionId, CancellationToken cancellationToken)
        {
            //Facilities.VendorVersionId is a restricted foreign key, so a referenced version cannot
            //be deleted at all. Soft-deleted facilities keep the column set and so still hold it;
            //counting them is what the database does, and leaving them out would return a 204 the
            //delete never achieved.
            var facilityCount = await _dbContext.Facilities
                .CountAsync(facility => facility.VendorVersionId == vendorVersionId, cancellationToken);

            if (facilityCount > 0)
            {
                _logger.LogInformation(
                    "Refused to delete vendor version {VendorVersionId}: {FacilityCount} facility configuration(s) are assigned to it.",
                    vendorVersionId,
                    facilityCount);

                throw new VendorVersionInUseException(
                    $"Vendor version '{vendorVersionId}' is assigned to {facilityCount} facility " +
                    $"configuration{(facilityCount == 1 ? string.Empty : "s")} and cannot be deleted.");
            }
        }

        private async Task EnsureVendorVersionIsNotReferencedByNormalizationAsync(Guid vendorVersionId, CancellationToken cancellationToken)
        {
            var response = await _normalizationServiceClient.GetVendorVersionOperationPresetsAsync(
                vendorVersionId: vendorVersionId,
                cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Unable to verify Normalization references before deleting the vendor version.");
            }

            if (response.Body?.Count > 0)
            {
                throw new VendorVersionInUseException(
                    $"Vendor version '{vendorVersionId}' is referenced by Normalization and cannot be deleted.");
            }
        }

        /// <summary>
        /// Runs a delete and turns a foreign key refusal into <see cref="VendorVersionInUseException"/>,
        /// which the controllers answer with 409. The checks above cannot close the window between
        /// themselves and the delete - another request may assign the version to a facility in it -
        /// and this also keeps a reference added later from reaching the client as a 500.
        /// </summary>
        private static async Task DeleteAndTranslateStillReferencedAsync(Func<Task<int>> delete, string message)
        {
            try
            {
                await delete();
            }
            catch (Exception ex) when (IsStillReferenced(ex))
            {
                throw new VendorVersionInUseException(message, ex);
            }
        }

        //EF wraps the provider's exception, at times several levels deep, so walk the chain. SQL
        //Server raises 547 for a foreign key violation. SQLite, which the integration tests run on,
        //reports a constraint violation as primary code 19 and an extended code that varies by how
        //the statement reached it (1811 here, not the 787 the documentation suggests); the primary
        //code is enough, because the only constraint a DELETE can break is a reference to the row.
        private static bool IsStillReferenced(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException { Number: 547 }
                    || current is SqliteException { SqliteErrorCode: 19 })
                {
                    return true;
                }
            }

            return false;
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

            existingVendor.Authentication = vendor.Authentication ?? existingVendor.Authentication;

            _dbContext.Vendors.Update(existingVendor);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new VendorModel
            {
                Id = existingVendor.Id,
                Name = existingVendor.Name,
                Authentication = existingVendor.Authentication
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