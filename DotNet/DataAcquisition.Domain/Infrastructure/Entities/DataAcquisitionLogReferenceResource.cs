using System.ComponentModel.DataAnnotations;
namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

/// <summary>
/// Junction table linking <see cref="DataAcquisitionLog"/> to <see cref="ReferenceResources"/>.
/// Configured as a hidden mapping entity via <c>UsingEntity</c> in the DbContext;
/// both sides expose skip-navigations (<c>DataAcquisitionLog.ReferenceResources</c>
/// and <c>ReferenceResources.DataAcquisitionLogs</c>) that EF Core populates automatically.
/// </summary>
public class DataAcquisitionLogReferenceResource
{
    public long DataAcquisitionLogId { get; set; }
    public Guid ReferenceResourceId { get; set; }
}
