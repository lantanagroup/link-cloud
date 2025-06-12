using LantanaGroup.Link.Tests.BackendE2ETests.ApiRequests;
using LantanaGroup.Link.Tests.E2ETests;
using RestSharp;
using TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.BackendE2ETests.Tests
{
    public sealed class SingleMeasureAdHocReportingSmokeTests(ITestOutputHelper output)
    {       
        [Fact]
        [Trait("Category", "AdHocSingleMeasureSmokeTest")]
        public async Task SmokeTest_GenerateSingleMeasureAdHocReport()
        {
            var adminBffClient = new RestClient(TestConfig.AdminBffBase);
            AdHocReportApiRequests apiE2E = new AdHocReportApiRequests(output);
            SubmissionZipReader submissionReportZip = new SubmissionZipReader(output);
            AdhocReportingSmokeTest adhocReportingSmokeTest = new AdhocReportingSmokeTest(output);
            MeasureLoader measureLoader = new MeasureLoader(adminBffClient, output);

            await adhocReportingSmokeTest.InitializeAsync();    
            apiE2E.Create_SingleMeasureAdHocTestFacility(); 
            await measureLoader.LoadAsync();                    
            apiE2E.Create_SingleMeasureCensusConfiguration_AdHoc();     
            apiE2E.Create_SingleMeasureQueryDispatchConfig_AdHoc();     
            apiE2E.Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc();      
            apiE2E.Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc();     
            apiE2E.Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc();      
            apiE2E.Create_SingleMeasureFHIRQueryListByFacility_AdHoc();    
            apiE2E.Create_SingleMeasureFacilityNormalizationConfig_AdHoc();     
            apiE2E.GenerateSingleMeasureAdHocReport_ACH();      

            var failures = new List<string>();
            try
            {
                await submissionReportZip.WaitForSingleMeasureZipContentsAsync();
                await submissionReportZip.DownloadAndExtractSingleMeasureZipAsync();
                ValidationHelper.TryRunValidation(submissionReportZip.SingleMeasureAdHocValidateFilesAppear, failures);
                ValidationHelper.TryRunValidation(submissionReportZip.SingleMeasureAdHocValidateFilesDoNotAppear, failures);
                ValidationHelper.TryRunValidation(() => submissionReportZip.ValidateSpecificPatientFileContents(3, 2000), failures);
                ValidationHelper.TryRunValidation(submissionReportZip.ValidateSingleMeasureAdHocAggregateACHFile, failures);
                apiE2E.GETSingleMeasureAdHocFacilityValidationResultsForReport();
                await adhocReportingSmokeTest.DisposeAsync();
            }
            finally
            {
                if (failures.Any())
                {
                    output.WriteLine("🔴 ================= TEST RESULT SUMMARY =================🔴 ");
                    foreach (var fail in failures)
                    output.WriteLine(fail);

                    Xunit.Assert.Fail($"{failures.Count} verification(s) failed. See console output below.");
                }
                output.WriteLine("[PASS] Smoke test completed with all verifications passing.");
            }
        }
    }
}
