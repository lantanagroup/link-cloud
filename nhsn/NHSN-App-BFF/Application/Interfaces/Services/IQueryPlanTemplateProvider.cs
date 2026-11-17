using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Loads the static, per-vendor QueryPlan template used to auto-seed a facility's DataAcquisition
// QueryPlan (see QueryPlanAutoSeedSettings). Mirrors PackageZipDownloadService's vendor-keyed
// StaticAssets lookup.
public interface IQueryPlanTemplateProvider
{
    Task<CreateQueryPlanRequestApiModel?> GetTemplateAsync(EhrVendor vendor, CancellationToken cancellationToken = default);
}
