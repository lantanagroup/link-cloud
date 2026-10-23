namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// How long ago the admission or discharge happened. Mirrors Link's DataAcquisition TimeFrame.
/// </summary>
public enum CensusTimeFrame
{
    LessThan24Hours,
    Between24To48Hours,
    MoreThan48Hours
}
