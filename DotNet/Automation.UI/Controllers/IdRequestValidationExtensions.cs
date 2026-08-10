using Automation.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

internal static class IdRequestValidationExtensions
{
    internal static bool TryValidateIdRequest(this Controller controller, IdRequest? request, out IActionResult badRequest)
    {
        if (request == null)
        {
            badRequest = controller.BadRequest("Request body is required.");
            return false;
        }

        if (!controller.ModelState.IsValid)
        {
            badRequest = controller.BadRequest(controller.ModelState);
            return false;
        }

        if (request.Id == Guid.Empty)
        {
            controller.ModelState.AddModelError(nameof(IdRequest.Id), "Id must be a non-empty GUID.");
            badRequest = controller.BadRequest(controller.ModelState);
            return false;
        }

        badRequest = default!;
        return true;
    }
}
