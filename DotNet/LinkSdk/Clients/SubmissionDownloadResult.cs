namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// Result of copying a submission ZIP into a caller-supplied stream.
/// The body is not buffered. <see cref="ErrorBody"/> is set only when the
/// response is not a success, and it is the error payload rather than a ZIP.
/// </summary>
public sealed record SubmissionDownloadResult(int StatusCode, string? ErrorBody)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}
