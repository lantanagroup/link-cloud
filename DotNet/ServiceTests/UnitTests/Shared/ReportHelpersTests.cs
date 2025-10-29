using LantanaGroup.Link.Shared.Application.Utilities;

namespace ServiceTests.UnitTests.Shared
{
    public class ReportHelpersTests
    {
        #region Input Validation Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetReportName_InvalidScheduleId_ThrowsArgumentException(string scheduleId)
        {
            // Arrange
            var facilityId = "TestFacility";
            var reportTypes = new List<string> { "TestReport" };
            var reportStartDate = DateTime.Now;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate));
            Assert.Equal("scheduleID", exception.ParamName);
            Assert.Contains("Schedule ID cannot be null or empty", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetReportName_InvalidFacilityId_ThrowsArgumentException(string facilityId)
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var reportTypes = new List<string> { "TestReport" };
            var reportStartDate = DateTime.Now;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate));
            Assert.Equal("facilityId", exception.ParamName);
            Assert.Contains("Facility ID cannot be null or empty", exception.Message);
        }

        [Fact]
        public void GetReportName_NullReportTypes_ThrowsArgumentException()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            List<string> reportTypes = null;
            var reportStartDate = DateTime.Now;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate));
            Assert.Equal("reportTypes", exception.ParamName);
            Assert.Contains("Report types cannot be null or empty", exception.Message);
        }

        [Fact]
        public void GetReportName_EmptyReportTypes_ThrowsArgumentException()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportTypes = new List<string>();
            var reportStartDate = DateTime.Now;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate));
            Assert.Equal("reportTypes", exception.ParamName);
            Assert.Contains("Report types cannot be null or empty", exception.Message);
        }

        #endregion

        #region Case Sensitivity Tests

        [Fact]
        public void GetReportName_FacilityIdCaseVariations_NormalizesToLowercase()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var reportTypes = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation" };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result1 = ReportHelpers.GetReportName(scheduleId, "TestFacility", reportTypes, reportStartDate);
            var result2 = ReportHelpers.GetReportName(scheduleId, "TESTFACILITY", reportTypes, reportStartDate);
            var result3 = ReportHelpers.GetReportName(scheduleId, "testfacility", reportTypes, reportStartDate);

            // Assert
            Assert.Equal(result1, result2);
            Assert.Equal(result1, result3);
            Assert.StartsWith("testfacility_", result1);
        }

        [Fact]
        public void GetReportName_ReportTypesCaseVariations_NormalizesToLowercase()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result1 = ReportHelpers.GetReportName(scheduleId, facilityId, new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation" }, reportStartDate);
            var result2 = ReportHelpers.GetReportName(scheduleId, facilityId, new List<string> { "NHSNDQMACUTECAREHOSPITALINITIALPOPULATION" }, reportStartDate);

            // Assert - Both should produce the same result since MeasureNameShortener maps to "ACH" then lowercased to "ach"
            Assert.Equal(result1, result2);
            Assert.Contains("testfacility_ach_20240115_", result1);
        }

        #endregion

        #region MeasureNameShortener Integration Tests

        [Theory]
        [InlineData("NHSNdQMAcuteCareHospitalInitialPopulation", "ach")]
        [InlineData("NHSNGlycemicControlHypoglycemicInitialPopulation", "hypo")]
        [InlineData("NHSNRespiratoryPathogensSurveillanceInitialPopulation", "rps")]
        [InlineData("NHSNAcuteCareHospitalMonthlyInitialPopulation", "achm")]
        [InlineData("NHSNAcuteCareHospitalDailyInitialPopulation", "achd")]
        public void GetReportName_KnownReportTypes_UsesCorrectShortening(string reportType, string expectedShortened)
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportTypes = new List<string> { reportType };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert
            Assert.Contains($"testfacility_{expectedShortened}_20240115_", result);
        }

        [Fact]
        public void GetReportName_UnknownReportType_UsesOriginalNameLowercased()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var unknownReportType = "UnknownReportType";
            var reportTypes = new List<string> { unknownReportType };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert
            Assert.Contains($"testfacility_{unknownReportType.ToLowerInvariant()}_20240115_", result);
        }

        #endregion

        #region ReportTypes Order and Duplicates Tests

        [Fact]
        public void GetReportName_ReportTypesOrderIndependent_ProducesSameResult()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportTypes1 = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation", "NHSNGlycemicControlHypoglycemicInitialPopulation" };
            var reportTypes2 = new List<string> { "NHSNGlycemicControlHypoglycemicInitialPopulation", "NHSNdQMAcuteCareHospitalInitialPopulation" };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result1 = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes1, reportStartDate);
            var result2 = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes2, reportStartDate);

            // Assert
            Assert.Equal(result1, result2);
            // Should be sorted: "ach" + "hypo" = "ach+hypo"
            Assert.Contains("testfacility_ach+hypo_20240115_", result1);
        }

        [Fact]
        public void GetReportName_MultipleReportTypes_JoinedWithPlus()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportTypes = new List<string>
            {
                "NHSNRespiratoryPathogensSurveillanceInitialPopulation", // -> RPS -> rps
                "NHSNdQMAcuteCareHospitalInitialPopulation", // -> ACH -> ach
                "NHSNGlycemicControlHypoglycemicInitialPopulation" // -> Hypo -> hypo
            };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert - Should be alphabetically sorted: ach, hypo, rps
            Assert.Contains("testfacility_ach+hypo+rps_20240115_", result);
        }

        #endregion

        #region Date Handling Tests

        [Fact]
        public void GetReportName_WithStartDate_IncludesFormattedDate()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportTypes = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation" };
            var reportStartDate = new DateTime(2024, 3, 15);

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert
            Assert.Contains("_20240315_", result);
        }

        [Fact]
        public void GetReportName_WithoutStartDate_ExcludesDate()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportTypes = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation" };

            // Act
            var result1 = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, null);
            var result2 = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, new DateTime(2024, 3, 15));

            // Assert
            Assert.NotEqual(result1, result2);
            Assert.DoesNotContain("20240315", result1);
            Assert.Contains("20240315", result2);

            // Without date: facilityId_reportType_hash
            Assert.Matches(@"^testfacility_ach_[A-Za-z0-9_-]+$", result1);
            // With date: facilityId_reportType_date_hash  
            Assert.Matches(@"^testfacility_ach_20240315_[A-Za-z0-9_-]+$", result2);
        }

        #endregion

        #region Hash Stability and Deterministic Output Tests

        [Fact]
        public void GetReportName_SameInputs_ProducesSameOutput()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TestFacility";
            var reportTypes = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation", "NHSNGlycemicControlHypoglycemicInitialPopulation" };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result1 = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);
            var result2 = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);
            var result3 = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert
            Assert.Equal(result1, result2);
            Assert.Equal(result1, result3);
        }

        [Fact]
        public void GetReportName_DifferentScheduleIds_ProduceDifferentHashes()
        {
            // Arrange
            var facilityId = "TestFacility";
            var reportTypes = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation" };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result1 = ReportHelpers.GetReportName("Schedule1", facilityId, reportTypes, reportStartDate);
            var result2 = ReportHelpers.GetReportName("Schedule2", facilityId, reportTypes, reportStartDate);

            // Assert
            Assert.NotEqual(result1, result2);
            // The hash portion (last part after final underscore) should be different
            var hash1 = result1.Split('_').Last();
            var hash2 = result2.Split('_').Last();
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void GetReportName_KnownInputs_ProducesExpectedStructure()
        {
            // Arrange - Using deterministic inputs to ensure platform stability
            var scheduleId = "SCHED-12345";
            var facilityId = "FAC001";
            var reportTypes = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation" };
            var reportStartDate = new DateTime(2024, 1, 1);

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);

            var parts = result.Split('_');
            Assert.Equal(4, parts.Length); // facilityId_reportType_date_hash
            Assert.Equal("fac001", parts[0]); // lowercase facility ID
            Assert.Equal("ach", parts[1]); // shortened report type
            Assert.Equal("20240101", parts[2]); // formatted date
            Assert.NotEmpty(parts[3]); // hash part should be present and non-empty

            // Hash should be URL-safe base64 (no padding, + -> -, / -> _)
            Assert.Matches(@"^[A-Za-z0-9_-]+$", parts[3]);
        }

        [Fact]
        public void GetReportName_ScheduleIdIsAppended_ContainsScheduleId()
        {
            // Arrange
            var scheduleId = "-SpecialChars";
            var facilityId = "TestFacility";
            var reportTypes = new List<string> { "NHSNdQMAcuteCareHospitalInitialPopulation" };
            var reportStartDate = new DateTime(2024, 1, 15);

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert
            var scheduleIdPart = result.Split('_').Last();
            Assert.Equal("-specialchars", scheduleIdPart); // Schedule ID is lowercased and appended
        }

        #endregion

        #region Edge Case Integration Tests

        [Fact]
        public void GetReportName_MixedCaseReportTypesWithUnknownType_HandlesCorrectly()
        {
            // Arrange
            var scheduleId = "TestSchedule123";
            var facilityId = "TESTFAC";
            var reportTypes = new List<string>
            {
                "NHSNDQMACUTECAREHOSPITALINITIALPOPULATION", // Known type (case insensitive)
                "UnknownCustomReportType" // Unknown type
            };
            var reportStartDate = new DateTime(2024, 6, 30);

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, reportStartDate);

            // Assert
            Assert.StartsWith("testfac_", result);
            Assert.Contains("_20240630_", result);
            // Should be sorted: "ach" + "unknowncustomreporttype" = "ach+unknowncustomreporttype"
            Assert.Contains("ach+unknowncustomreporttype", result);
        }

        [Fact]
        public void GetReportName_SingleCharacterInputs_HandlesCorrectly()
        {
            // Arrange
            var scheduleId = "A";
            var facilityId = "B";
            var reportTypes = new List<string> { "C" };

            // Act
            var result = ReportHelpers.GetReportName(scheduleId, facilityId, reportTypes, null);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("b_c_", result);
            var parts = result.Split('_');
            Assert.Equal(3, parts.Length); // facilityId_reportType_hash (no date)
        }

        #endregion
    }
}
