using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("Logs")]
public sealed class LogsController : Controller
{
    private readonly LogsService _logs;

    public LogsController(LogsService logs)
    {
        _logs = logs;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Logs";
        return View(_logs.LoadHome());
    }

    [HttpGet("Acquisition")]
    public async Task<IActionResult> Acquisition(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Acquisition log";
        ViewData["LogsSection"] = "acquisition";
        return View(await _logs.LoadAcquisitionAsync(query, cancellationToken));
    }

    [HttpGet("Acquisition/{id:long}")]
    public async Task<IActionResult> AcquisitionDetail(long id, int refPage, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Acquisition log";
        ViewData["LogsSection"] = "acquisition";
        return View(await _logs.LoadAcquisitionDetailAsync(id, refPage, cancellationToken));
    }

    [HttpPost("Acquisition/process")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(AcquisitionQuery query, List<long>? ids, CancellationToken cancellationToken)
    {
        var result = await _logs.ProcessSelectedAsync(ids, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/process-matching")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessMatching(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        var result = await _logs.ProcessMatchingAsync(query, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(AcquisitionQuery query, List<long>? ids, CancellationToken cancellationToken)
    {
        var result = await _logs.CancelSelectedAsync(ids, query.MinAgeHours, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/cancel-matching")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMatching(AcquisitionQuery query, CancellationToken cancellationToken)
    {
        var result = await _logs.CancelMatchingAsync(query, cancellationToken);
        return Back(query, result);
    }

    [HttpPost("Acquisition/{id:long}/process")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessOne(long id, CancellationToken cancellationToken)
    {
        var result = await _logs.ProcessSelectedAsync([id], cancellationToken);
        Temp(result);
        return RedirectToAction(nameof(AcquisitionDetail), new { id });
    }

    [HttpPost("Acquisition/{id:long}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOne(long id, int minAgeHours, CancellationToken cancellationToken)
    {
        var result = await _logs.CancelSelectedAsync([id], minAgeHours, cancellationToken);
        Temp(result);
        return RedirectToAction(nameof(AcquisitionDetail), new { id });
    }

    [HttpGet("Sftp")]
    public async Task<IActionResult> Sftp(SftpQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "SFTP acquisition log";
        ViewData["LogsSection"] = "sftp";
        return View(await _logs.LoadSftpAsync(query, cancellationToken));
    }

    [HttpGet("Sftp/{id:guid}")]
    public async Task<IActionResult> SftpDetail(string id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "SFTP acquisition log";
        ViewData["LogsSection"] = "sftp";
        return View(await _logs.LoadSftpDetailAsync(id, cancellationToken));
    }

    [HttpPost("Sftp/{id:guid}/reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetSftp(string id, CancellationToken cancellationToken)
    {
        var result = await _logs.ResetSftpAsync(id, cancellationToken);
        Temp(result);
        return RedirectToAction(nameof(SftpDetail), new { id });
    }

    [HttpGet("Audit")]
    public async Task<IActionResult> Audit(AuditQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Audit event log";
        ViewData["LogsSection"] = "audit";
        return View(await _logs.LoadAuditAsync(query, cancellationToken));
    }

    [HttpGet("Audit/{id:guid}")]
    public async Task<IActionResult> AuditDetail(string id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Audit event";
        ViewData["LogsSection"] = "audit";
        return View(await _logs.LoadAuditDetailAsync(id, cancellationToken));
    }

    [HttpGet("Kafka")]
    public IActionResult Kafka()
    {
        ViewData["Title"] = "Kafka";
        ViewData["LogsSection"] = "kafka";
        return View(_logs.LoadKafka());
    }

    private IActionResult Back(AcquisitionQuery query, LogsAction result)
    {
        Temp(result);
        var route = new RouteValueDictionary();
        foreach (var pair in query.ToRoute())
            route[pair.Key] = pair.Value;
        return RedirectToAction(nameof(Acquisition), route);
    }

    private void Temp(LogsAction result)
    {
        if (result.Succeeded)
            TempData["Message"] = result.Message;
        else
            TempData["Error"] = result.Message;
    }
}
