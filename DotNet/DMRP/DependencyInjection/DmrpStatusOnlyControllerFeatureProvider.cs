using LantanaGroup.Link.DMRP.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace LantanaGroup.Link.DMRP.DependencyInjection;

/// <summary>
/// With DMRP disabled, drops every module controller except <see cref="DmrpStatusController"/> from discovery.
/// </summary>
/// <remarks>
/// The other controllers' services are not registered while the module is off, so leaving them routable
/// would turn their requests into 500s instead of 404s. The status controller needs only the settings and
/// the mock status, which the disabled module still registers.
/// </remarks>
internal sealed class DmrpStatusOnlyControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        var moduleAssembly = typeof(DmrpModuleExtensions).Assembly;

        var hidden = feature.Controllers
            .Where(c => c.Assembly == moduleAssembly)
            .Where(c => c.AsType() != typeof(DmrpStatusController))
            .ToList();

        foreach (var controller in hidden)
        {
            feature.Controllers.Remove(controller);
        }
    }
}
