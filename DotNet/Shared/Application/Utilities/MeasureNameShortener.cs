namespace LantanaGroup.Link.Shared.Application.Utilities;

public static class MeasureNameShortener
{
    public static string ShortenMeasureName(string measureName)
    {
        return measureName.ToLowerInvariant() switch
        {
            "nhsnacutecarehospitalmonthlyinitialpopulation" => "ACHM",
            "nhsnacutecarehospitaldailyinitialpopulation" => "ACHD",
            "nhsndqmacutecarehospitalinitialpopulation" => "ACH",
            "nhsnglycemiccontrolhypoglycemicinitialpopulation" => "Hypo",
            "nhsnrespiratorypathogenssurveillanceinitialpopulation" => "RPS",
            _ => measureName
        };
    }
}