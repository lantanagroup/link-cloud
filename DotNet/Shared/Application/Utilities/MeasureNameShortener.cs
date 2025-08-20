namespace LantanaGroup.Link.Shared.Application.Utilities;

public static class MeasureNameShortener
{
    public static string ShortenMeasureName(string measureName)
    {
        return measureName switch
        {
            "NHSNAcuteCareHospitalMonthlyInitialPopulation" => "ACHM",
            "NHSNAcuteCareHospitalDailyInitialPopulation" => "ACHD",
            "NHSNdQMAcuteCareHospitalInitialPopulation" => "ACH",
            "NHSNGlycemicControlHypoglycemicInitialPopulation" => "Hypo",
            "NHSNRespiratoryPathogensSurveillanceInitialPopulation" => "RPS",
            _ => measureName
        };
    }
}