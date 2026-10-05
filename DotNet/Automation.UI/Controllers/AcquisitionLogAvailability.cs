namespace Automation.UI.Controllers;

/// <summary>
/// Decides whether a Data Acquisition response is a normal page, a missing
/// source, or a failed load. An empty list is a normal page. A success with
/// no body, including HTTP 204, is a missing source.
/// </summary>
internal readonly record struct AcquisitionLogRead(bool Unavailable, int? ErrorStatus);

internal static class AcquisitionLogAvailability
{
    public static AcquisitionLogRead Classify(int? statusCode, bool hasBody, bool notFoundIsMissing = false)
    {
        if (hasBody)
            return new AcquisitionLogRead(false, null);

        if (statusCode is >= 200 and < 300)
            return new AcquisitionLogRead(true, null);

        if (notFoundIsMissing && statusCode == StatusCodes.Status404NotFound)
            return new AcquisitionLogRead(true, null);

        var status = statusCode is null or < 100
            ? StatusCodes.Status502BadGateway
            : statusCode.Value;
        return new AcquisitionLogRead(false, status);
    }
}
