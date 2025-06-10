using LantanaGroup.Link.Tests.BackendE2ETests.Pages_Services;
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
            AdHocReportApiRequests apiE2E = new AdHocReportApiRequests(output);
            SubmissionZipReader submissionReportZip = new SubmissionZipReader(output);

            var failures = new List<string>();
            try
            {
                apiE2E.Create_SingleMeasureAdHocTestFacility();
                apiE2E.UpdateMeasureDefinition();
                apiE2E.Create_SingleMeasureCensusConfiguration_AdHoc();
                apiE2E.Create_SingleMeasureQueryDispatchConfig_AdHoc();
                apiE2E.Create_SingleMeasure_FHIRQueryConfigByFacility_AdHoc();
                apiE2E.Create_SingleMeasure_MontlhyQueryPlanByFacility_AdHoc();
                apiE2E.Create_SingleMeasure_DischargeQueryPlanByFacility_AdHoc();
                apiE2E.Create_SingleMeasureFHIRQueryListByFacility_AdHoc();
                apiE2E.Create_SingleMeasureFacilityNormalizationConfig_AdHoc();
                apiE2E.GenerateSingleMeasureAdHocReport_ACH();
                await submissionReportZip.WaitForSingleMeasureZipContentsAsync();
                await submissionReportZip.DownloadAndExtractSingleMeasureZipAsync();
                ValidationHelper.TryRunValidation(submissionReportZip.SingleMeasureAdHocValidateFilesAppear, failures);
                ValidationHelper.TryRunValidation(submissionReportZip.SingleMeasureAdHocValidateFilesDoNotAppear, failures);
                ValidationHelper.TryRunValidation(submissionReportZip.ValidatePatientHypoAPR2FileContents, failures);
                ValidationHelper.TryRunValidation(submissionReportZip.ValidateSingleMeasureAdHocAggregateACHFile, failures);
                apiE2E.GETSingleMeasureAdHocFacilityValidationResultsForReport();
            }
            finally
            {
                if (failures.Any())
                {
                    output.WriteLine("========== TEST RESULT SUMMARY ==========");
                    foreach (var fail in failures)
                    output.WriteLine(fail);

                    Xunit.Assert.Fail($"{failures.Count} verification(s) failed. See console output below.");
                }

                output.WriteLine("[PASS] Smoke test completed with all verifications passing.");
            }
        }
    }
}
