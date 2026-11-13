namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// One NHSN measure the facility is enrolled in AND MeasureEval can actually evaluate -- the
// single source both the Reporting Plan step's completion gate and the Generate Test Report
// step's picker consume.
public record AvailableMeasure
{
    public required string Name { get; init; }

    public required string DigitalQualityMeasure { get; init; }
}
