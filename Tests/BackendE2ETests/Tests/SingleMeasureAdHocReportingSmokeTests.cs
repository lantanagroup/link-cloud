using LantanaGroup.Link.Tests.BackendE2ETests.Pages_Services;
using LantanaGroup.Link.Tests.E2ETests;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestHelper;


namespace LantanaGroup.Link.Tests.BackendE2ETests.Tests
{
    [TestClass]
    public class SingleMeasureAdHocReportingSmokeTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [TestCategory("SmokeTest_GenerateSingleMeasureAdHocReport")]
        public async Task SmokeTest_GenerateSingleMeasureAdHocReport()
        {
            var apiE2E = new AdHocReportApiRequests
            {
                TestContext = this.TestContext
            };

            var submissionReportZip = new SubmissionZipReader
            {
                TestContext = this.TestContext
            };
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
                    TestContext.WriteLine("========== TEST RESULT SUMMARY ==========");
                    foreach (var fail in failures)
                        TestContext.WriteLine(fail);

                    Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail($"{failures.Count} verification(s) failed. See console output below.");
                }

                TestContext.WriteLine("[PASS] Smoke test completed with all verifications passing.");
            }
        }
    }
}
