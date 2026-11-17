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
            //the vendor's name comes back with its version ids, in the read that was already being
            //made: a refusal has to name the vendor the administrator picked off the list, and the id
            //identifies the row without telling them which one it is.
            var existing = await _dbContext.Vendors
                .Where(vendor => vendor.Id == vendorId)
                .Select(vendor => new
                {
                    vendor.Name,
                    Versions = vendor.VendorVersions
                        .Select(version => new { version.Id, version.Version })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            List<Guid> vendorVersionIds = existing?.Versions.Select(version => version.Id).ToList() ?? [];

            //deleting the vendor cascades to its versions, so a facility holding any one of them
            //fails the same foreign key. One read covers every version, and the refusal names the
            //vendor the caller asked to delete rather than a version id they never saw.
            var facilities = await DescribeFacilitiesUsingAsync(vendorVersionIds, withVersion: true, cancellationToken);
            if (facilities.Count > 0)
            {
                throw InUse(DescribeVendor(existing?.Name, vendorId), facilities, vendorId, nameof(DeleteVendorAsync));
            }

            if (existing is not null)
            {
                foreach (var version in existing.Versions)
                {
                    await EnsureVendorVersionIsNotReferencedByNormalizationAsync(
                        version.Id,
                        DescribeVendorVersion(existing.Name, version.Version, version.Id),
                        cancellationToken);
                }
            }

            await DeleteAndTranslateStillReferencedAsync(
                () => _dbContext.Vendors.Where(q => q.Id == vendorId).ExecuteDeleteAsync(cancellationToken),
                $"{DescribeVendor(existing?.Name, vendorId)} is still referenced by a facility configuration and cannot be deleted.");
        }

        public async Task DeleteVendorVersionAsync(Guid vendorVersionId, CancellationToken cancellationToken = default)
        {
            //the version string and its vendor's name, so a refusal names what the administrator is
            //looking at: version '2026.1' of vendor 'Epic', not the id the row happens to carry.
            var existing = await _dbContext.VendorVersions
                .Where(vendorVersion => vendorVersion.Id == vendorVersionId)
                .Select(vendorVersion => new
                {
                    vendorVersion.Version,
                    VendorName = vendorVersion.Vendor!.Name
                })
                .FirstOrDefaultAsync(cancellationToken);

            var subject = DescribeVendorVersion(existing?.VendorName, existing?.Version, vendorVersionId);

            //the local reference comes first: one indexed read against the service's own database,
            //before an HTTP call to Normalization that a facility-held version makes pointless.
            var facilities = await DescribeFacilitiesUsingAsync([vendorVersionId], withVersion: false, cancellationToken);
            if (facilities.Count > 0)
            {
                throw InUse(subject, facilities, vendorVersionId, nameof(DeleteVendorVersionAsync));
            }

            await EnsureVendorVersionIsNotReferencedByNormalizationAsync(vendorVersionId, subject, cancellationToken);

            await DeleteAndTranslateStillReferencedAsync(
                () => _dbContext.VendorVersions.Where(q => q.Id == vendorVersionId).ExecuteDeleteAsync(cancellationToken),
                $"{subject} is still referenced by a facility configuration and cannot be deleted.");
        }

        /// <summary>
        /// The facilities holding any of these vendor versions, described for a refusal message.
        /// Soft-deleted facilities keep their VendorVersionId, so the row still holds the foreign key
        /// and the database would refuse the delete anyway; they are named as deleted rather than left
        /// out, because an administrator cannot find one in the UI and would read the refusal as a bug.
        /// </summary>
        private async Task<List<string>> DescribeFacilitiesUsingAsync(
            IReadOnlyCollection<Guid> vendorVersionIds,
            bool withVersion,
            CancellationToken cancellationToken)
        {
            if (vendorVersionIds.Count == 0)
            {
                return [];
            }

            var facilities = await _dbContext.Facilities
                .Where(facility => facility.VendorVersionId != null
                    && vendorVersionIds.Contains(facility.VendorVersionId.Value))
                .OrderBy(facility => facility.FacilityId)
                .Select(facility => new
                {
                    facility.FacilityId,
                    facility.FacilityName,
                    facility.IsDeleted,
                    Version = facility.VendorVersion!.Version
                })
                .ToListAsync(cancellationToken);

            //"ABC123 (Mercy General, version 2026.1)", or "ABC123 (deleted)" - the name is optional,
            //and the version only earns its place when the refusal spans more than one of them.
            return facilities
                .Select(facility =>
                {
                    var parts = new List<string>(3);
                    if (!string.IsNullOrWhiteSpace(facility.FacilityName))
                    {
                        parts.Add(facility.FacilityName);
                    }

                    if (facility.IsDeleted)
                    {
                        parts.Add("deleted");
                    }

                    if (withVersion && !string.IsNullOrWhiteSpace(facility.Version))
                    {
                        parts.Add($"version {facility.Version}");
                    }

                    return parts.Count == 0
                        ? facility.FacilityId
                        : $"{facility.FacilityId} ({string.Join(", ", parts)})";
                })
                .ToList();
        }

        /// <summary>
        /// A vendor as the administrator being refused sees it: by the name on the row they picked. The
        /// id identifies the row and stays in the log line, where it is the thing worth searching on,
        /// and only stands in here when the row is gone - another request may have deleted it.
        /// </summary>
        private static string DescribeVendor(string? name, Guid vendorId) =>
            string.IsNullOrWhiteSpace(name)
                ? $"Vendor '{vendorId}'"
                : $"Vendor '{name}'";

        /// <summary>
        /// A version named the way the UI lists it, which is by its vendor: "Version '2026.1' of
        /// vendor 'Epic'". The version string alone is not unique across vendors, so the vendor's name
        /// goes with it whenever it is known.
        /// </summary>
        private static string DescribeVendorVersion(string? vendorName, string? version, Guid vendorVersionId)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return $"Vendor version '{vendorVersionId}'";
            }

            return string.IsNullOrWhiteSpace(vendorName)
                ? $"Vendor version '{version}'"
                : $"Version '{version}' of vendor '{vendorName}'";
        }

        /// <summary>
        /// A count alone leaves an administrator hunting, so the refusal names the facilities. Capped,
        /// because this message reaches the client on Problem Details `detail`; the count stays exact.
        /// </summary>
        private VendorVersionInUseException InUse(string subject, List<string> facilities, Guid id, string operation)
        {
            const int maxNamed = 5;
            var named = string.Join(", ", facilities.Take(maxNamed));
            if (facilities.Count > maxNamed)
            {
                named += $" and {facilities.Count - maxNamed} more";
            }

            _logger.LogInformation(
                "{Operation} refused for {Id}: in use by {FacilityCount} facility configuration(s) ({Facilities}).",
                operation,
                id,
                facilities.Count,
                HtmlInputSanitizer.Sanitize(named));

            return new VendorVersionInUseException(
                $"{subject} is in use by {facilities.Count} facility " +
                $"configuration{(facilities.Count == 1 ? string.Empty : "s")} and cannot be deleted: {named}. " +
                "Clear the vendor version on each facility before deleting it.");
        }

        private async Task EnsureVendorVersionIsNotReferencedByNormalizationAsync(Guid vendorVersionId, string subject, CancellationToken cancellationToken)
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
                    $"{subject} is referenced by Normalization and cannot be deleted.");
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