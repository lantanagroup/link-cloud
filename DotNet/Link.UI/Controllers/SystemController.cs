using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("System")]
public sealed class SystemController : Controller
{
    private readonly SystemService _system;

    public SystemController(SystemService system)
    {
        _system = system;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "System";
        return View(_system.LoadHome());
    }

    [HttpGet("Themes")]
    [HttpGet("/themes")]
    public IActionResult Themes()
    {
        ViewData["Title"] = "Themes";
        return View();
    }

    [HttpGet("Users")]
    public async Task<IActionResult> Users(UserQuery query, CancellationToken cancellationToken)
    {
        Section("users", "Accounts");
        return View(await _system.LoadUsersAsync(query, cancellationToken));
    }

    [HttpGet("Users/{id}")]
    public async Task<IActionResult> Account(string id, CancellationToken cancellationToken)
    {
        Section("users", "Account");
        return View(await _system.LoadUserAsync(id, cancellationToken));
    }

    [HttpPost("Users")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(UserForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.SaveUserAsync(null, form, cancellationToken));
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("Users/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(string id, UserForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.SaveUserAsync(id, form, cancellationToken));
        return RedirectToAction(nameof(Account), new { id });
    }

    [HttpPost("Users/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
    {
        Temp(await _system.DeleteUserAsync(id, cancellationToken));
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("Users/{id}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreUser(string id, CancellationToken cancellationToken)
    {
        Temp(await _system.RecoverUserAsync(id, cancellationToken));
        return RedirectToAction(nameof(Account), new { id });
    }

    [HttpPost("Users/{id}/claims")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUserClaims(string id, ClaimForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.SaveUserClaimsAsync(id, form, cancellationToken));
        return RedirectToAction(nameof(Account), new { id });
    }

    [HttpGet("Roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        Section("roles", "Roles");
        return View(await _system.LoadRolesAsync(cancellationToken));
    }

    [HttpPost("Roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRole(RoleForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.SaveRoleAsync(form, cancellationToken));
        return RedirectToAction(nameof(Roles));
    }

    [HttpPost("Roles/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(string id, CancellationToken cancellationToken)
    {
        Temp(await _system.DeleteRoleAsync(id, cancellationToken));
        return RedirectToAction(nameof(Roles));
    }

    [HttpPost("Roles/{id}/claims")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRoleClaims(string id, ClaimForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.SaveRoleClaimsAsync(id, form, cancellationToken));
        return RedirectToAction(nameof(Roles));
    }

    [HttpGet("Health")]
    public async Task<IActionResult> Health(string? service, CancellationToken cancellationToken)
    {
        Section("health", "Service health");
        return View(await _system.LoadHealthAsync(service, cancellationToken));
    }

    [HttpGet("Health/status")]
    public async Task<IActionResult> HealthStatus(string? service, CancellationToken cancellationToken)
    {
        var page = await _system.LoadHealthAsync(service, includeServiceInfo: false, cancellationToken);
        return Json(new
        {
            error = page.HealthError,
            reports = page.Reports.Select(report => new
            {
                service = report.Service,
                status = report.Status,
                duration = report.Duration,
                hidden = report.HiddenEntries,
                entries = report.Entries.Select(entry => new
                {
                    name = entry.Name,
                    status = entry.Status,
                    duration = entry.Duration,
                    description = entry.Description
                })
            })
        });
    }

    [HttpGet("AppConfiguration")]
    public IActionResult AppConfiguration()
    {
        Section("configuration", "App configuration");
        return View(_system.LoadAppConfiguration());
    }

    [HttpGet("Integration")]
    public IActionResult Integration()
    {
        Section("integration", "Integration test");
        return View(_system.LoadIntegration());
    }

    [HttpPost("Integration/report-scheduled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleReport(ReportScheduledForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.ScheduleReportAsync(form, cancellationToken));
        return RedirectToAction(nameof(Integration));
    }

    [HttpPost("Integration/patient-list")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcquirePatientList(PatientListForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.AcquirePatientListAsync(form, cancellationToken));
        return RedirectToAction(nameof(Integration));
    }

    [HttpPost("Integration/patient-event")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostPatientEvent(PatientEventForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.PostPatientEventAsync(form, cancellationToken));
        return RedirectToAction(nameof(Integration));
    }

    [HttpPost("Integration/data-acquisition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostDataAcquisition(DataAcquisitionForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.PostDataAcquisitionAsync(form, cancellationToken));
        return RedirectToAction(nameof(Integration));
    }

    [HttpPost("Integration/patient-acquired")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostPatientAcquired(PatientAcquiredForm form, CancellationToken cancellationToken)
    {
        Temp(await _system.PostPatientAcquiredAsync(form, cancellationToken));
        return RedirectToAction(nameof(Integration));
    }

    [HttpPost("Integration/start-consumers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartConsumers(string? correlationId, CancellationToken cancellationToken)
    {
        Temp(await _system.StartConsumersAsync(correlationId, cancellationToken));
        return RedirectToAction(nameof(Integration));
    }

    [HttpPost("Integration/read-consumers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadConsumers(string? correlationId, CancellationToken cancellationToken)
    {
        var page = await _system.ReadConsumersAsync(correlationId, cancellationToken);
        if (page.ReadError is not null)
        {
            TempData["Error"] = page.ReadError;
            return RedirectToAction(nameof(Integration));
        }

        Section("integration", "Integration test");
        return View("Integration", page);
    }

    [HttpPost("Integration/stop-consumers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StopConsumers(string? correlationId, CancellationToken cancellationToken)
    {
        Temp(await _system.StopConsumersAsync(correlationId, cancellationToken));
        return RedirectToAction(nameof(Integration));
    }

    private void Section(string section, string title)
    {
        ViewData["Title"] = title;
        ViewData["SystemSection"] = section;
    }

    private void Temp(SystemAction result)
    {
        if (result.Succeeded)
            TempData["Message"] = result.Message;
        else
            TempData["Error"] = result.Message;
    }
}
