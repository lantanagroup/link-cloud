namespace LantanaGroup.Link.Shared.Application.Models;

public enum MeasureDefinitionType
{
    NHSNGlycemicControlHypoglycemicInitialPopulation,
    NHSNdQMAcuteCareHospitalInitialPopulation,
    NHSNRespiratoryPathogenSurveillanceInitialPopulation,
    AdultPatients
}

public static class MeasureDefinitionTypeValidation
{
    public static bool Validate(string str)
    {
        try
        {         
            var value = (MeasureDefinitionType)Enum.Parse(typeof(MeasureDefinitionType), str);
            var enumVals = Enum.GetValues(typeof(MeasureDefinitionType)).Cast<MeasureDefinitionType>();
            return enumVals.Any(val => val == value);
        }
        catch(Exception)
        {
            return false;
        }
    }
}