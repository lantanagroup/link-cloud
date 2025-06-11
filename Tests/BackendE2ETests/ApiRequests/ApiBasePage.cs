using LantanaGroup.Link.Tests.E2ETests;

namespace API_Integration.Pages
{
    public class ApiBasePage
    {
        protected static readonly string api_LinkAdminBffURL = TestConfig.AdminBffBase; 
        protected static readonly string fhirServerBaseUrl = TestConfig.InternalFhirServerBase;

        public const string AdHocSmokeTestFile = "Stu3-AdHocSmokeTest";
        public const string SingleMeasureAdHocFacility = "SingleMeasureAdHocFacility";
        public const string SingleMeasureAdHocAchDqmVersion = "0.0.014";
        public const string MeasureAch = "NHSNdQMAcuteCareHospitalInitialPopulation";
        public const string CronValue = "0 0 */4 * * ?";
    }
}
