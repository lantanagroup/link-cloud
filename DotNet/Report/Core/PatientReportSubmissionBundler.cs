using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSubmission.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Queries;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using MediatR;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Core
{
    /// <summary>
    /// This Class is used to generate a bundle of a particular patients data for the provided facility and the report period.
    /// This bundle will include data for all applicable Measure Reports as well as a separate bundle of all resources that are not strictly "Patient" resources.
    /// </summary>
    public class PatientReportSubmissionBundler
    {
        private readonly ILogger<PatientReportSubmissionBundler> _logger;
        private readonly IMediator _mediator;
        private readonly IReportServiceMetrics _metrics;

        private readonly List<string> REMOVE_EXTENSIONS = new List<string> {
        "http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.population.description",
        "http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalDataElement.reference",
        "http://hl7.org/fhir/us/davinci-deqm/StructureDefinition/extension-criteriaReference",
        "http://open.epic.com/FHIR/StructureDefinition/extension/accidentrelated",
        "http://open.epic.com/FHIR/StructureDefinition/extension/epic-id",
        "http://open.epic.com/FHIR/StructureDefinition/extension/ip-admit-datetime",
        "http://open.epic.com/FHIR/StructureDefinition/extension/observation-datetime",
        "http://open.epic.com/FHIR/StructureDefinition/extension/specialty",
        "http://open.epic.com/FHIR/StructureDefinition/extension/team-name",
        "https://open.epic.com/FHIR/StructureDefinition/extension/patient-merge-unmerge-instant"};

        public List<string> PatientResourceTypes = new List<string>()
        {
            "Account", "AdverseEvent", "AllergyIntolerance", "Appointment", "AppointmentResponse", "AuditEvent",
            "Basic", "BodyStructure", "CarePlan", "CareTeam", "ChargeItem", "Claim", "ClaimResponse",
            "ClinicalImpression", "Communication", "CommunicationRequest", "Composition", "Condition", "Consent",
            "Coverage", "CoverageEligibilityRequest", "CoverageEligibilityResponse", "DetectedIssue", "DeviceRequest",
            "DeviceUseStatement", "DiagnosticReport", "DocumentManifest", "DocumentReference", "Encounter",
            "EnrollmentRequest", "EpisodeOfCare", "ExplanationOfBenefit", "FamilyMemberHistory", "Flag", "Goal",
            "Group", "ImagingStudy", "Immunization", "ImmunizationEvaluation", "ImmunizationRecommendation", "Invoice",
            "List", "MeasureReport", "Media", "MedicationAdministration", "MedicationDispense", "MedicationRequest",
            "MedicationStatement", "MolecularSequence", "NutritionOrder", "Observation", "Patient", "Person",
            "Procedure", "Provenance", "QuestionnaireResponse", "RelatedPerson", "RequestGroup", "ResearchSubject",
            "RiskAssessment", "Schedule", "ServiceRequest", "Specimen", "SupplyDelivery", "SupplyRequest",
            "VisionPrescription"
        };

        public PatientReportSubmissionBundler(ILogger<PatientReportSubmissionBundler> logger, IMediator mediator, IReportServiceMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
            _metrics = metrics ?? throw new ArgumentException(nameof(metrics));
        }


        public async Task<PatientSubmissionModel> GenerateBundle(string facilityId, string patientId, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrEmpty(facilityId))
                throw new Exception($"GenerateBundle: no facilityId supplied");

            if (string.IsNullOrEmpty(patientId))
                throw new Exception($"GenerateBundle: no patientId supplied");

            var entries = await _mediator.Send(new GetPatientSubmissionEntriesQuery() { FacilityId = facilityId, PatientId = patientId, StartDate = startDate, EndDate = endDate });

            Bundle patientResourceBundle = CreateNewBundle();
            Bundle otherResources = CreateNewBundle();  
            foreach (var entry in entries)
            {
                var measureReportScheduleId = entry.MeasureReportScheduleId;
                var schedule = await _mediator.Send(new GetMeasureReportScheduleQuery { Id = measureReportScheduleId });
                if (schedule == null)
                    throw new Exception($"No report schedule found for measureReportScheduleId {measureReportScheduleId}");

                var submission = await _mediator.Send(new FindMeasureReportSubmissionByScheduleQuery { MeasureReportScheduleId = measureReportScheduleId });
                Bundle bundle = submission == null ? CreateNewBundle() : submission.SubmissionBundle;

                var parser = new FhirJsonParser();
                MeasureReport mr;
                try
                {
                    mr = parser.Parse<MeasureReport>(entry.MeasureReport);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{nameof(MeasureReportSubmissionModel)} with ID {entry.Id} could not be parsed into a valid MeasureReport.", ex);
                    continue;
                }

                var config = (await _mediator.Send(new SearchMeasureReportConfigQuery { FacilityId = facilityId, ReportType = schedule.ReportType })).FirstOrDefault();

                if (config == null)
                    throw new Exception($"No report configs found for Facility {schedule.FacilityId}");

                // ensure aggregate patient list and measure report entries are created for reach measure
                var org = bundle.Entry.FirstOrDefault(e => e.Resource.TypeName == "Organization"
                                                           && (e.Resource.Meta is not null && e.Resource.Meta.Profile is not null
                                                               && e.Resource.Meta.Profile.Contains(ReportConstants.Bundle.SubmittingOrganizationProfile))
                );

                string orgId = org?.Resource.Id ?? "";

                if (entry.ContainedResources is not null && entry.ContainedResources.Count > 0)
                {
                    if (mr.Contained == null) mr.Contained = new List<Resource>();

                    entry.ContainedResources.ForEach(r =>
                    {
                        Resource resource = null!;
                        if (r.Resource == null) return;
                        try
                        {
                            resource = parser.Parse<Resource>(r.Resource);
                            mr.Contained.Add(resource);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"{resource.TypeName} with ID {resource?.Id} contained resource could not be parsed into a valid Resource.", ex);
                        }
                    });
                }

                // ensure we have an id to reference
                if (string.IsNullOrEmpty(mr.Id))
                    mr.Id = Guid.NewGuid().ToString();
                // ensure we have a meta object
                // set individual measure report profile
                mr.Meta = new Meta
                {
                    Profile = new List<string> { ReportConstants.Bundle.IndividualMeasureReportProfileUrl }
                };

                // clean up resource
                cleanupResource(mr);
                // Clean up the contained resources within the measure report
                cleanupContainedResource(mr);

                if (config != null && config.BundlingType == BundlingType.SharedPatientLineLevel)
                    BundleSharedPatientLineLevel(bundle, mr);
                else
                    BundleDefault(bundle, mr);

                foreach (var resource in bundle.GetResources())
                {
                    if (PatientResourceTypes.Contains(resource.TypeName))
                    {
                        patientResourceBundle.AddResourceEntry(resource, GetFullUrl(resource));
                    }
                    else
                    {
                        otherResources.AddResourceEntry(resource, GetFullUrl(resource));
                    }
                }

                _metrics.IncrementReportGeneratedCounter(new List<KeyValuePair<string, object?>>() {
                    new KeyValuePair<string, object?>("facilityId", schedule.FacilityId),
                    new KeyValuePair<string, object?>("measure.schedule.id", measureReportScheduleId),
                    new KeyValuePair<string, object?>("submitting.organization", orgId),
                    new KeyValuePair<string, object?>("measure", mr.Measure),
                    new KeyValuePair<string, object?>("bundling.type", config?.BundlingType)
                });
            }

            PatientSubmissionModel patientSubmissionModel = new PatientSubmissionModel()
            {
                FacilityId = facilityId,
                PatientId = patientId,
                StartDate = startDate,
                EndDate = endDate,
                PatientResources = patientResourceBundle,
                OtherResources = otherResources
            };

            return patientSubmissionModel;
        }

        private void cleanupContainedResource(MeasureReport mr)
        {
            mr.Contained.ForEach(cr =>
            {
                if (cr.Id != null && cr.Id.StartsWith("LCR-"))
                {
                    // Remove the LCR- prefix added by CQL
                    cr.Id = cr.Id.Substring(4);

                }
                else if (cr.Id != null && cr.Id.StartsWith("#LCR-"))
                {
                    // Remove the LCR- prefix added by CQL
                    cr.Id = cr.Id.Substring(5);
                }
                // update references in the evaluated resource to point to the contained reference (for validation purposes)
                mr.EvaluatedResource.Where(er => er.Reference != null && er.Reference == GetRelativeReference(cr)).ToList().ForEach(er => er.Reference = "#" + cr.Id);

            });
        }




        #region Bundling Options

        /// <summary>
        /// Adds the given MeasureReport to the given Bundle with the default (option B) method which leaves contained resources left in tact
        /// </summary>
        /// <param name="bundle">Bundle to add the MeasureReport to</param>
        /// <param name="measureReport">MeasureReport to be added to the given Bundle</param>
        /// <returns>new Bundle with MeasureReport added</returns>
        private void BundleDefault(Bundle bundle, MeasureReport measureReport)
        {
            _logger.LogDebug($"Adding MeasureReport to bundle using default bundling method.");

            // add or update the measure report as-is to the bundle
            AddMeasureReportToBundle(bundle, measureReport);
        }


        private void cleanupResource(Resource resource)
        {
            if (resource is DomainResource)
            {
                DomainResource domainResource = (DomainResource)resource;

                // Remove extensions from resources
                domainResource.Extension.RemoveAll(e => e.Url != null && REMOVE_EXTENSIONS.Contains(e.Url));

                // Remove extensions from group/populations of MeasureReports
                if (resource is MeasureReport)
                {
                    MeasureReport measureReport = (MeasureReport)resource;
                    measureReport.Group.ForEach(g =>
                    {
                        g.Population.ForEach(p =>
                        {
                            p.Extension.RemoveAll(e => e.Url != null && REMOVE_EXTENSIONS.Contains(e.Url));
                        });
                    });
                    measureReport.EvaluatedResource.ForEach(er =>
                    {
                        er.Extension.RemoveAll(e => e.Url != null && REMOVE_EXTENSIONS.Contains(e.Url));

                    });

                }
            }
        }


        /// <summary>
        /// Adds the given MeasureReport to the given Bundle with the patient line level (option A) method which pulls out contained resources
        /// </summary>
        /// <param name="bundle">Bundle to add the MeasureReport to</param>
        /// <param name="measureReport">MeasureReport to be added to the given Bundle</param>
        /// <returns>new Bundle with MeasureReport added</returns>
        private void BundleSharedPatientLineLevel(Bundle bundle, MeasureReport measureReport)
        {
            _logger.LogInformation($"Adding MeasureReport to bundle using shared patient line level bundling method.");

            // add the measure report to the bundle first
            AddMeasureReportToBundle(bundle, measureReport);


            // pull each contained resource from the measure report and add it to the top level bundle removing it from the contained list after it has been copied
            while (measureReport.Contained.Count > 0)
            {
                var resource = measureReport.Contained[0];

                // check for existing resource in the bundle with same type and id
                var existingEntryIndex = bundle.Entry.FindIndex(e => e.Resource.TypeName == resource.TypeName && e.Resource.Id == resource.Id);
                if (existingEntryIndex < 0)
                {
                    // no duplicate in the bundle so we can just add it
                    bundle.AddResourceEntry(resource, GetFullUrl(resource));
                }
                else
                {
                    Resource existingResource = bundle.Entry[existingEntryIndex].Resource;

                    // if this is not an exact match to the existing resource then we have to merge
                    if (!resource.IsExactly(existingResource))
                    {
                        // TODO: determine how to merge based on resource type (?)
                        _logger.LogError($"Need to merge duplicate \"{GetRelativeReference(resource)}\"");

                        // for now... update existing completely
                        //bundle.Entry[existingEntryIndex] = new Bundle.EntryComponent() { Resource = resource, FullUrl = GetFullUrl(resource) };
                    }
                }

                //  change the resource reference in the measure report to point to the top level bundle
                var found = measureReport.EvaluatedResource.FirstOrDefault(e => e.Reference != null && e.Reference == "#" + resource.Id);
                if (found != null) found.Reference = GetRelativeReference(resource);

                measureReport.Contained.RemoveAt(0);

            }

        }

        #endregion


        #region Common Methods

        protected MeasureReport ReportFormatter(MeasureReport measureReport)
        {
            measureReport.EvaluatedResource.ForEach(r => { if (r.Extension.Count > 0) r.Extension = new List<Extension>(); });
            return measureReport;
        }

        protected Bundle CreateNewBundle()
        {
            Bundle bundle = new Bundle();
            bundle.Meta = new Meta
            {
                Profile = new string[] { ReportConstants.Bundle.ReportBundleProfileUrl },
                Tag = new List<Coding> { new Coding(ReportConstants.Bundle.MainSystem, "report", "Report") }
            };
            bundle.Identifier = new Identifier(ReportConstants.Bundle.IdentifierSystem, "urn:uuid:" + Guid.NewGuid());
            bundle.Type = Bundle.BundleType.Collection;
            bundle.Timestamp = DateTime.UtcNow;

            return bundle;
        }


        protected string GetRelativeReference(Resource resource)
        {
            return string.Format("{0}/{1}", resource.TypeName, resource.Id);
        }

        protected string GetFullUrl(Resource resource)
        {
            return string.Format(ReportConstants.Bundle.BundlingFullUrlFormat, GetRelativeReference(resource));
        }

        protected string GetMeasureIdFromCanonical(string measureCanonical)
        {
            if (string.IsNullOrWhiteSpace(measureCanonical))
            {
                throw new Exception("Error in GetMeasureIdFromCanonical: measureCanonical parameter is null or whitespace");
            }

            int index = measureCanonical.LastIndexOf('/');

            int versionIndex = measureCanonical.LastIndexOf('|');

            // get the measure between index and versionIndex
            if (versionIndex > 0)
            {
                return measureCanonical.Substring(index + 1, versionIndex - index - 1);
            }
            else
            {
                // no version specified
                return measureCanonical.Substring(index + 1);
            }
        }

        protected Bundle.EntryComponent GetAggregateMeasureReport(Bundle bundle, string measureCanonical)
        {

            return bundle.Entry.FirstOrDefault(e => e.Resource.TypeName == "MeasureReport"
                && ((MeasureReport)e.Resource).Measure == measureCanonical
                && e.Resource.Meta is not null && e.Resource.Meta.Profile is not null
                && e.Resource.Meta.Profile.Contains(ReportConstants.Bundle.SubjectListMeasureReportProfile)
            );
        }

        protected MeasureReport CreateAggregateMeasureReport(string measureCanonical, string organizationId, FhirDateTime reportStart, FhirDateTime reportEnd)
        {
            MeasureReport mr = new MeasureReport();
            mr.Id = Guid.NewGuid().ToString();
            mr.Meta = new Meta
            {
                Profile = new List<string> { ReportConstants.Bundle.SubjectListMeasureReportProfile }
            };
            mr.Contained = new List<Resource>
            {
                new List() { Status = List.ListStatus.Current, Mode = ListMode.Snapshot, Id =  (mr.Contained.Count+1).ToString() }
            };
            mr.Status = MeasureReport.MeasureReportStatus.Complete;
            mr.Type = MeasureReport.MeasureReportType.SubjectList;

            mr.Measure = measureCanonical;
            // remove version from measureCanonical
            int versionIndex = measureCanonical.LastIndexOf('|');
            if (versionIndex > 0)
            {
                mr.Measure = measureCanonical.Substring(0, versionIndex);
            }

            mr.Reporter = new ResourceReference($"Organization/{organizationId}");
            mr.Period = new Period(reportStart, reportEnd);

            return mr;
        }

        /// <summary>
        /// Adds the given measure report to the aggregate measure report for its corresponding measure in the given bundle if not already present
        /// </summary>
        protected void AddToAggregateMeasureReport(Bundle submissionBundle, MeasureReport measureReport, string organizationId)
        {
            var measureAgg = GetAggregateMeasureReport(submissionBundle, measureReport.Measure);

            // ensure we have an aggregate measure report for this measure
            if (measureAgg is null)
            {
                measureAgg = new Bundle.EntryComponent() { Resource = CreateAggregateMeasureReport(measureReport.Measure, organizationId, new FhirDateTime(measureReport.Period.Start), new FhirDateTime(measureReport.Period.End)) };
                submissionBundle.Entry.Add(measureAgg);
            }


            List aggList = (List)((MeasureReport)measureAgg.Resource).Contained.First(c => c.TypeName == "List");
            string mrReference = $"MeasureReport/{measureReport.Id}";
            var existing = aggList.Entry.FirstOrDefault(e => e.Item.Reference == mrReference);

            if (existing is null)
            {
                aggList.Entry.Add(new List.EntryComponent() { Item = new ResourceReference(mrReference) });
            }

            MeasureReport masterReport = (MeasureReport)measureAgg.Resource;
            // add group
            foreach (MeasureReport.GroupComponent group in measureReport.Group)
            {
                foreach (MeasureReport.PopulationComponent population in group.Population)
                {

                    // Check if group and population code exist in master, if not create
                    MeasureReport.PopulationComponent measureGroupPopulation = getOrCreateGroupAndPopulation(masterReport, population, group);
                    // Add population.count to the master group/population count
                    //if measureGroupPopulation.Count is null return 0
                    measureGroupPopulation.Count = (measureGroupPopulation.Count != null ? measureGroupPopulation.Count : 0) + (population.Count != null ? population.Count : 0);
                    // If this population incremented the master
                    if (population.Count > 0)
                    {
                        // set a reference to the aggList
                        measureGroupPopulation.SubjectResults = new ResourceReference($"#{aggList.Id}");
                    }

                }
            }

        }

        protected MeasureReport.PopulationComponent getOrCreateGroupAndPopulation(MeasureReport masterReport, MeasureReport.PopulationComponent reportPopulation, MeasureReport.GroupComponent reportGroup)
        {
            // get the population and group codes

            string populationCode = (reportPopulation.Code != null && reportPopulation.Code.Coding.Count > 0) ? reportPopulation.Code.Coding[0].Code : "";
            string groupCode = (reportGroup.Code != null && reportGroup.Code.Coding.Count > 0) ? reportGroup.Code.Coding[0].Code : "";

            MeasureReport.GroupComponent masterReportGroupValue = null;
            MeasureReport.PopulationComponent masteReportGroupPopulationValue;
            // find the group by code
            MeasureReport.GroupComponent masterReportGroup;
            masterReportGroup = masterReport.Group.FirstOrDefault(g => g.Code != null && g.Code.Coding.Count > 0 && g.Code.Coding[0].Code == groupCode);
            // if empty find the group without the code
            if (masterReportGroup != null)
            {
                masterReportGroupValue = masterReportGroup;
            }
            else
            {
                if (groupCode == "")
                {
                    masterReportGroupValue = (masterReport.Group != null && masterReport.Group.Count > 0) ? masterReport.Group[0] : null; // only one group with no code
                }
            }
            // if still empty create it
            if (masterReportGroupValue == null)
            {
                masterReportGroupValue = new MeasureReport.GroupComponent();
                masterReportGroupValue.Code = (reportGroup.Code != null ? reportGroup.Code : null);
                masterReport.Group.Add(masterReportGroupValue);
            }
            // find population by code
            MeasureReport.PopulationComponent masterReportGroupPopulation = masterReportGroupValue.Population.FirstOrDefault(g => g.Code != null && g.Code.Coding.Count > 0 && g.Code.Coding[0].Code == populationCode);
            // if empty create it
            if (masterReportGroupPopulation != null)
            {
                masteReportGroupPopulationValue = masterReportGroupPopulation;
            }
            else
            {
                masteReportGroupPopulationValue = new MeasureReport.PopulationComponent();
                masteReportGroupPopulationValue.Code = reportPopulation.Code;
                masterReportGroupValue.Population.Add(masteReportGroupPopulationValue);
            }
            return masteReportGroupPopulationValue;
        }

        /// <summary>
        /// Adds the given measure report to the given bundle.
        /// If an existing report exists with the same ID in the bundle, then the provided report will replace the existing report.
        /// </summary>
        /// <returns>Bundle.EntryComponent with the newly added measure report</returns>
        protected Bundle.EntryComponent AddMeasureReportToBundle(Bundle bundle, MeasureReport measureReport)
        {
            Bundle.EntryComponent entry;

            // find existing matching measure report
            var existingEntryIndex = bundle.Entry.FindIndex(e => e.Resource.TypeName == "MeasureReport" && e.Resource.Id == measureReport.Id);
            if (existingEntryIndex < 0)
            {
                // doesn't exist... add it to the bundle as-is
                entry = bundle.AddResourceEntry(measureReport, GetFullUrl(measureReport));
            }
            else
            {
                // already exists in bundle... update the entry
                entry = bundle.Entry[existingEntryIndex] = new Bundle.EntryComponent() { Resource = measureReport, FullUrl = GetFullUrl(measureReport) };
            }

            return entry;
        }

        #endregion

    }

}
