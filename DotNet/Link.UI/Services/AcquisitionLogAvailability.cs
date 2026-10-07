using Microsoft.AspNetCore.Http;

namespace Link.UI.Services;

/// <summary>
/// Decides whether a Data Acquisition response is a normal page, a missing
/// source, or a failed load. An empty list is a normal page. A success with
/// no body, including HTTP 204, is a missing source.
/// </summary>
public readonly record struct AcquisitionLogRead(bool Unavailable, int? ErrorStatus);

public static class AcquisitionLogAvailability
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

    /// <summary>
    /// A fallback replaces the first response when it has rows, or when the
    /// first response had no body and the fallback has one. A failed fallback
    /// does not replace a bodyless success.
    /// </summary>
    public static bool PreferFallback(bool initialHasBody, int fallbackRecordCount, bool fallbackHasBody)
        => fallbackRecordCount > 0 || (!initialHasBody && fallbackHasBody);
}
