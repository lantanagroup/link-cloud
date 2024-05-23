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
