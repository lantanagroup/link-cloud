using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Services;

/// <summary>
/// Copies a temp file to a destination asynchronously and then deletes it.
/// </summary>
public static class TempFileResponse
{
    public static async Task CopyAndDeleteAsync(string path, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            await source.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

/// <summary>
/// Sends a temp file as a download and deletes it after the copy.
/// </summary>
public sealed class DeleteAfterSendFileResult : IActionResult
{
    private readonly string _path;
    private readonly string _contentType;
    private readonly string _fileName;

    public DeleteAfterSendFileResult(string path, string contentType, string fileName)
    {
        _path = path;
        _contentType = contentType;
        _fileName = fileName;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = _contentType;
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = _fileName
        }.ToString();

        if (File.Exists(_path))
            response.ContentLength = new FileInfo(_path).Length;

        await TempFileResponse.CopyAndDeleteAsync(_path, response.Body, context.HttpContext.RequestAborted);
    }
}
