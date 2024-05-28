namespace LantanaGroup.Link.Report.Domain.Enums
{
    public static class PatientResourceProvider
    {
        public static List<string> GetPatientResourceTypes()
        {
            return Enum.GetNames(typeof(PatientResourceType)).OfType<string>().ToList();
        }
    }
}
