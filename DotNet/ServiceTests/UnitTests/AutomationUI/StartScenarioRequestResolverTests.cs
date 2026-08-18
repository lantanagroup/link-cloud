using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class StartScenarioRequestResolverTests
{
    // ---------- Non-Custom scenarios use scenario-kind defaults outright ----------

    [Theory]
    [InlineData(AutomationScenarioKind.AdhocReportTest, 1, 1000, 20260326, 0)]
    [InlineData(AutomationScenarioKind.MultiPatientTest, 1000, 100, 20260328, 0)]
    public void Non_custom_scenarios_use_scenario_kind_defaults(
        AutomationScenarioKind scenario, int expectedPatientCount, int expectedResources, int expectedSeed, int expectedMaxPollingDurationMinutes)
    {
        var request = new StartScenarioRequest
        {
            Scenario = scenario,
            // These overrides should be ignored for non-Custom scenarios.
            PatientCount = 999,
            ResourcesPerPatient = 999,
            Seed = 999,
        };

        var options = StartScenarioRequestResolver.Resolve(request);

        options.PatientCount.Should().Be(expectedPatientCount);
        options.ResourcesPerPatient.Should().Be(expectedResources);
        options.Seed.Should().Be(expectedSeed);
        options.MaxPollingDurationMinutes.Should().Be(expectedMaxPollingDurationMinutes);
    }

    [Fact]
    public void Mega_patient_uses_FhirBundleGenerator_default_sizes()
    {
        var options = StartScenarioRequestResolver.Resolve(
            new StartScenarioRequest { Scenario = AutomationScenarioKind.MegaPatientTest });

        options.PatientCount.Should().Be(FhirBundleGenerator.DefaultPatientCount);
        options.ResourcesPerPatient.Should().Be(FhirBundleGenerator.DefaultResourcesPerPatient);
    }

    [Fact]
    public void Unknown_scenario_kind_throws()
    {
        var request = new StartScenarioRequest { Scenario = (AutomationScenarioKind)int.MaxValue };

        var act = () => StartScenarioRequestResolver.Resolve(request);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------- Custom scenario: typed property overrides win ----------

    [Fact]
    public void Custom_scenario_typed_overrides_replace_defaults()
    {
        var request = new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            PatientCount = 7,
            Seed = 12345,
            CleanupServiceData = true,
            CleanupFhirData = false,
            ReportMethod = ReportMethod.RegenerateReport,
            QueryPlanTemplateId = Guid.NewGuid(),
            NhsnOrganizationId = "10756",
            SelectedMeasures = [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation],
        };

        var options = StartScenarioRequestResolver.Resolve(request);

        options.PatientCount.Should().Be(7);
        options.ResourcesPerPatient.Should().Be(250);
        options.Seed.Should().Be(12345);
        options.MaxPollingDurationMinutes.Should().Be(30);
        options.CleanupServiceData.Should().BeTrue();
        options.CleanupFhirData.Should().BeFalse();
        options.ReportMethod.Should().Be(ReportMethod.RegenerateReport);
        options.QueryPlanTemplateId.Should().Be(request.QueryPlanTemplateId);
        options.NhsnOrganizationId.Should().Be("10756");
        options.SelectedMeasures.Should().ContainSingle()
            .Which.Should().Be(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);
    }

    [Theory]
    [InlineData(AutomationScenarioKind.AdhocReportTest, "10756")]
    [InlineData(AutomationScenarioKind.MultiPatientTest, "10758")]
    [InlineData(AutomationScenarioKind.MegaPatientTest, "10759")]
    public void Non_custom_scenarios_default_to_distinct_static_nhsn_organization_ids(
        AutomationScenarioKind scenario,
        string expectedNhsnOrganizationId)
    {
        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = scenario,
        });

        options.NhsnOrganizationId.Should().Be(expectedNhsnOrganizationId);
    }

    [Fact]
    public void Custom_scenario_synthesizes_AllQualifying_cohort_when_none_supplied()
    {
        var request = new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            PatientCount = 5,
            ResourcesPerPatient = 100,
            SelectedMeasures = [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
        };

        var options = StartScenarioRequestResolver.Resolve(request);

        options.PatientCohorts.Should().ContainSingle();
        var cohort = options.PatientCohorts[0];
        cohort.PatientCount.Should().Be(5);
        cohort.GetEligibility(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            .Should().Be(MeasureEligibility.Qualifying);
        cohort.ResourcesPerPatientMin.Should().Be(250);
        cohort.ResourcesPerPatientMax.Should().Be(250);

        // Synthesized cohort should expand to PatientCount profiles.
        options.PatientProfiles.Should().HaveCount(5);
    }

    [Fact]
    public void Scheduled_report_defaults_missing_cohort_inpatient_pattern_to_first_pattern()
    {
        var json = """
            {
                "patientCohorts": [
                    {
                        "patientCount": 2,
                        "resourcesPerPatientMin": 50,
                        "resourcesPerPatientMax": 75,
                        "measureEligibilities": {
                            "NhsnAcuteCareHospitalMonthlyInitialPopulation": "Qualifying"
                        }
                    }
                ]
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            ReportMethod = ReportMethod.ScheduledReport,
            RunConfigurationJson = json,
        });

        options.PatientCohorts.Should().ContainSingle();
        options.PatientCohorts[0].ScheduledInpatientPattern
            .Should().Be(ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);

        options.PatientProfiles.Should().HaveCount(2);
        options.PatientProfiles.Should().OnlyContain(p =>
            p.ScheduledInpatientPattern == ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);
    }

    [Fact]
    public void Adhoc_report_also_defaults_missing_cohort_inpatient_pattern_to_first_pattern()
    {
        var json = """
            {
                "patientCohorts": [
                    {
                        "patientCount": 1,
                        "resourcesPerPatientMin": 50,
                        "resourcesPerPatientMax": 75,
                        "measureEligibilities": {
                            "NhsnAcuteCareHospitalMonthlyInitialPopulation": "Qualifying"
                        }
                    }
                ]
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            ReportMethod = ReportMethod.Adhoc,
            RunConfigurationJson = json,
        });

        options.PatientCohorts.Should().ContainSingle();
        options.PatientCohorts[0].ScheduledInpatientPattern
            .Should().Be(ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);
        options.PatientProfiles.Should().ContainSingle();
        options.PatientProfiles[0].ScheduledInpatientPattern
            .Should().Be(ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);
    }

    [Fact]
    public void Cohort_qualification_is_extracted_and_propagated_to_profiles()
    {
        var json = """
            {
                "patientCohorts": [
                    {
                        "patientCount": 1,
                        "cohortQualification": "NonQualifying",
                        "measureEligibilities": {
                            "NhsnAcuteCareHospitalMonthlyInitialPopulation": "Qualifying"
                        }
                    }
                ]
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        options.PatientCohorts.Should().ContainSingle();
        options.PatientCohorts[0].CohortQualification.Should().Be(MeasureEligibility.NonQualifying);
        options.PatientProfiles.Should().ContainSingle();
        options.PatientProfiles[0].CohortQualification.Should().Be(MeasureEligibility.NonQualifying);
    }

    // ---------- Custom scenario: JSON fallback when typed properties are missing ----------

    [Fact]
    public void Custom_scenario_extracts_selectedMeasures_from_RunConfigurationJson_when_request_list_empty()
    {
        // Quick Launch's hidden form posts only the JSON blob; without this fallback
        // every Quick-Launch run would silently revert to the scenario-kind default.
        var json = """
            {
                "selectedMeasures": [
                    "NhsnGlycemicControlHypoglycemicInitialPopulation",
                    "NhsnAcuteCareHospitalDailyInitialPopulation"
                ]
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        options.SelectedMeasures.Should().BeEquivalentTo(new[]
        {
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation,
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation,
        });
    }

    [Fact]
    public void Custom_scenario_extracts_patientCohorts_from_RunConfigurationJson()
    {
        var json = """
            {
                "patientCohorts": [
                    {
                        "patientCount": 3,
                        "resourcesPerPatientMin": 50,
                        "resourcesPerPatientMax": 75,
                        "measureEligibilities": {
                            "NhsnAcuteCareHospitalMonthlyInitialPopulation": "Qualifying"
                        },
                        "eligibleClinicalScenarioIds": ["1", "2"]
                    },
                    {
                        "patientCount": 2,
                        "resourcesPerPatientMin": 200,
                        "resourcesPerPatientMax": 200,
                        "measureEligibilities": {
                            "NhsnAcuteCareHospitalMonthlyInitialPopulation": "NonQualifying"
                        }
                    }
                ]
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        options.PatientCohorts.Should().HaveCount(2);
        options.PatientCohorts[0].PatientCount.Should().Be(3);
        options.PatientCohorts[0].EligibleClinicalScenarioIds.Should().BeEquivalentTo("1", "2");
        options.PatientCohorts[1].PatientCount.Should().Be(2);
        options.PatientCohorts[1]
            .GetEligibility(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            .Should().Be(MeasureEligibility.NonQualifying);

        // Profiles should be expanded for both cohorts (3 + 2 = 5).
        options.PatientProfiles.Should().HaveCount(5);
    }

    [Theory]
    [InlineData("\"Qualifying\"", MeasureEligibility.Qualifying)]
    [InlineData("\"NonQualifying\"", MeasureEligibility.NonQualifying)]
    [InlineData("\"qualifying\"", MeasureEligibility.Qualifying)] // case-insensitive
    [InlineData("0", MeasureEligibility.Qualifying)]              // legacy numeric
    [InlineData("1", MeasureEligibility.NonQualifying)]           // legacy numeric
    [InlineData("\"garbage\"", MeasureEligibility.Qualifying)]    // unknown string falls back to Qualifying
    public void Cohort_eligibility_accepts_string_and_numeric_representations(string jsonValue, MeasureEligibility expected)
    {
        var json = $$"""
            {
                "patientCohorts": [{
                    "patientCount": 1,
                    "measureEligibilities": {
                        "NhsnAcuteCareHospitalMonthlyInitialPopulation": {{jsonValue}}
                    }
                }]
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        options.PatientCohorts[0]
            .GetEligibility(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            .Should().Be(expected);
    }

    [Fact]
    public void Cohort_extraction_fills_missing_measure_eligibilities_with_Qualifying()
    {
        // The cohort JSON only mentions ACH-Monthly; ACH-Daily is selected too and must
        // not be left missing (downstream code reads every selected measure).
        var json = """
            {
                "patientCohorts": [{
                    "patientCount": 1,
                    "measureEligibilities": {
                        "NhsnAcuteCareHospitalMonthlyInitialPopulation": "Qualifying"
                    }
                }]
            }
            """;

        var request = new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            SelectedMeasures =
            [
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation,
            ],
            RunConfigurationJson = json,
        };

        var options = StartScenarioRequestResolver.Resolve(request);

        options.PatientCohorts[0].MeasureEligibilities
            .Should().ContainKey(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)
            .WhoseValue.Should().Be(MeasureEligibility.Qualifying);
    }

    [Fact]
    public void Custom_scenario_extracts_imported_patient_lists_from_json()
    {
        var json = """
            {
                "reportPeriodStart": "2024-03-01T00:00:00Z",
                "reportPeriodEnd":   "2024-03-31T23:59:59Z",
                "importedPatientIds": [
                    { "source": "ExistingId", "patientId": "abc" }
                ],
                "importedPatientBundles": [
                    { "source": "Bundle", "patientId": "xyz", "fileName": "f.json", "bundleJson": "{}" }
                ]
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        options.ImportedPatientIds.Should().ContainSingle()
            .Which.PatientId.Should().Be("abc");
        options.ImportedPatientBundles.Should().ContainSingle()
            .Which.PatientId.Should().Be("xyz");
    }

    [Fact]
    public void Imported_patients_require_explicit_report_period()
    {
        var json = """
            {
                "importedPatientIds": [
                    { "source": "ExistingId", "patientId": "abc" }
                ]
            }
            """;

        var act = () => StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Report period start and end are required when imported patients are included in a run.*");
    }

    [Fact]
    public void Custom_scenario_extracts_report_period_from_json_when_missing_on_request()
    {
        var json = """
            {
                "reportPeriodStart": "2024-03-01T00:00:00Z",
                "reportPeriodEnd":   "2024-03-31T23:59:59Z"
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        options.ReportPeriodStart.Should().Be(DateTimeOffset.Parse("2024-03-01T00:00:00Z"));
        options.ReportPeriodEnd.Should().Be(DateTimeOffset.Parse("2024-03-31T23:59:59Z"));
    }

    [Fact]
    public void Custom_scenario_extracts_nhsn_organization_id_from_json_when_missing_on_request()
    {
        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = """{ "nhsnOrganizationId": "22001" }"""
        });

        options.NhsnOrganizationId.Should().Be("22001");
    }

    [Fact]
    public void Request_report_period_wins_over_json()
    {
        var json = """{ "reportPeriodStart": "2020-01-01T00:00:00Z", "reportPeriodEnd": "2020-12-31T00:00:00Z" }""";
        var explicitStart = DateTimeOffset.Parse("2024-03-01T00:00:00Z");
        var explicitEnd = DateTimeOffset.Parse("2024-03-31T23:59:59Z");

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            ReportPeriodStart = explicitStart,
            ReportPeriodEnd = explicitEnd,
            RunConfigurationJson = json,
        });

        options.ReportPeriodStart.Should().Be(explicitStart);
        options.ReportPeriodEnd.Should().Be(explicitEnd);
    }

    // ---------- Resilience: malformed JSON must not crash the resolver ----------

    [Fact]
    public void Malformed_json_falls_back_to_defaults_without_throwing()
    {
        var act = () => StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = "{ not valid json",
        });

        var options = act.Should().NotThrow().Which;
        // Falls back to the synthesized AllQualifying cohort built from defaults.
        options.PatientCohorts.Should().NotBeEmpty();
        options.SelectedMeasures.Should()
            .Equal(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
    }

    [Fact]
    public void Empty_arrays_in_json_are_treated_as_absent()
    {
        var json = """
            {
                "selectedMeasures": [],
                "patientCohorts": [],
                "importedPatientIds": [],
                "importedPatientBundles": []
            }
            """;

        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = json,
        });

        // Falls through to defaults rather than producing an empty pool.
        options.SelectedMeasures.Should()
            .Equal(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
        options.PatientCohorts.Should().NotBeEmpty();
        options.ImportedPatientIds.Should().BeEmpty();
        options.ImportedPatientBundles.Should().BeEmpty();
    }

    [Fact]
    public void Live_simulation_forces_scheduled_report_and_extends_polling()
    {
        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            ReportMethod = ReportMethod.Adhoc,
            IsLiveSimulation = true,
            ReportingWindowMinutes = 15,
            SeedPatientCount = 2
        });

        options.IsLiveSimulation.Should().BeTrue();
        options.ReportMethod.Should().Be(ReportMethod.ScheduledReport);
        options.ReportingWindowMinutes.Should().Be(15);
        options.SeedPatientCount.Should().Be(2);
        options.MaxPollingDurationMinutes.Should().Be(45);
    }

    [Fact]
    public void Live_simulation_reads_flags_from_run_configuration_json()
    {
        var options = StartScenarioRequestResolver.Resolve(new StartScenarioRequest
        {
            Scenario = AutomationScenarioKind.Custom,
            RunConfigurationJson = """{ "isLiveSimulation": true, "reportingWindowMinutes": 7, "seedPatientCount": 4 }"""
        });

        options.IsLiveSimulation.Should().BeTrue();
        options.ReportMethod.Should().Be(ReportMethod.ScheduledReport);
        options.ReportingWindowMinutes.Should().Be(10);
        options.SeedPatientCount.Should().Be(4);
        options.MaxPollingDurationMinutes.Should().Be(40);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(0, 10)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    [InlineData(9, 10)]
    [InlineData(12, 15)]
    public void Reporting_window_minutes_are_normalized(int? input, int expected)
    {
        StartScenarioRequestResolver.NormalizeReportingWindowMinutes(input).Should().Be(expected);
    }
}
