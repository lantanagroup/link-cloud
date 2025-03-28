namespace LantanaGroup.Link.Shared.Application.Utilities;

public static class MeasureNameShortener
{
    public static string ShortenMeasureName(string measureName)
    {
        return measureName switch
        {
            "NHSNdQMAcuteCareHospitalInitialPopulation" => "ACH",
            "NHSNGlycemicControlHypoglycemicInitialPopulation" => "Hypo",
            "NHSNRespiratoryPathogensSurveillanceInitialPopulation" => "RPS",
            _ => "Unknown"
        };
    }
}