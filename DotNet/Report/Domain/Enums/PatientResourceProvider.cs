namespace LantanaGroup.Link.Report.Domain.Enums
{
    public static class PatientResourceProvider
    {
        public static List<string> GetPatientResourceTypes()
        {
            return Enum.GetValues(typeof(PatientResourceType)).OfType<string>().ToList();
        }
    }
}
