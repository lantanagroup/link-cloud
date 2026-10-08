namespace LantanaGroup.Link.DataAcquisition.Jobs;

/// <summary>
/// Decides whether a UTC time of day is inside a facility acquisition window.
/// A min later than max crosses midnight. Both ends are inclusive.
/// A min equal to max matches only that instant. Both null means unrestricted.
/// </summary>
public static class AcquisitionWindow
{
    public static bool Contains(TimeSpan? minAcquisitionPullTime, TimeSpan? maxAcquisitionPullTime, TimeSpan currentTime)
    {
        if (minAcquisitionPullTime == default && maxAcquisitionPullTime == default)
            return true;

        if (minAcquisitionPullTime <= maxAcquisitionPullTime)
            return currentTime >= minAcquisitionPullTime && currentTime <= maxAcquisitionPullTime;

        return currentTime >= minAcquisitionPullTime || currentTime <= maxAcquisitionPullTime;
    }
}
