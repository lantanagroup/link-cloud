﻿using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Queries;
using LantanaGroup.Link.Report.Application.PatientResource.Queries;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Application.SharedResource.Queries;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using MediatR;

namespace LantanaGroup.Link.Report.Core
{
    /// <summary>
    /// This Class is used to generate a bundleSettings of a particular patients data for the provided facility and the report period.
    /// This bundleSettings will include data for all applicable Measure Reports as well as a separate bundleSettings of all resources that are not strictly "Patient" resources.
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

            Bundle patientResources = CreateNewBundle();
            Bundle otherResources = CreateNewBundle();  
            foreach (var entry in entries)
            {
                var measureReportScheduleId = entry.MeasureReportScheduleId;
                var schedule = await _mediator.Send(new GetMeasureReportScheduleQuery { Id = measureReportScheduleId });
                if (schedule == null)
                    throw new Exception($"No report schedule found for measureReportScheduleId {measureReportScheduleId}");

                if (entry.MeasureReport == null) 
                {
                    continue;
                }

                MeasureReport mr = entry.MeasureReport;                

                var config = (await _mediator.Send(new SearchMeasureReportConfigQuery { FacilityId = facilityId, ReportType = schedule.ReportType })).FirstOrDefault();

                if (config == null)
                    throw new Exception($"No report configs found for Facility {schedule.FacilityId}");


                entry?.ContainedResources?.ForEach(async r =>
                {
                    if (r.DocumentId == null) return;

                    IFacilityResource facilityResource = null!;
                    
                    var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(r.ResourceType);

                    Resource resource = null;

                    try
                    {
                        if (resourceTypeCategory == ResourceCategoryType.Patient)
                        {
                            facilityResource = await _mediator.Send(new GetPatientResourceCommand(r.DocumentId));
                            resource = facilityResource.GetResource();
                            AddResourceToBundle(patientResources, resource);
                        }
                        else
                        {
                            facilityResource = await _mediator.Send(new GetSharedResourceCommand(r.DocumentId));
                            resource = facilityResource.GetResource();
                            AddResourceToBundle(otherResources, resource);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{resource.TypeName} with ID {resource?.Id} contained resource could not be parsed into a valid Resource.", ex);
                    }
                });
                

                // ensure we have an id to reference
                if (string.IsNullOrEmpty(mr.Id))
                    mr.Id = Guid.NewGuid().ToString();
                // ensure we have a meta object
                // set individual measure report profile
                mr.Meta = new Meta
                {
                    Profile = new List<string> { ReportConstants.BundleSettings.IndividualMeasureReportProfileUrl }
                };

                // clean up resource
                cleanupResource(mr);

                AddResourceToBundle(patientResources, mr);

                _metrics.IncrementReportGeneratedCounter(new List<KeyValuePair<string, object?>>() {
                    new KeyValuePair<string, object?>("facilityId", schedule.FacilityId),
                    new KeyValuePair<string, object?>("measure.schedule.id", measureReportScheduleId),
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
                PatientResources = patientResources,
                OtherResources = otherResources
            };

            return patientSubmissionModel;
        }

        #region Bundling Options

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
        #endregion


        #region Common Methods

        protected Bundle CreateNewBundle()
        {
            Bundle bundle = new Bundle();
            bundle.Meta = new Meta
            {
                Profile = new string[] { ReportConstants.BundleSettings.ReportBundleProfileUrl },
                Tag = new List<Coding> { new Coding(ReportConstants.BundleSettings.MainSystem, "report", "Report") }
            };
            bundle.Identifier = new Identifier(ReportConstants.BundleSettings.IdentifierSystem, "urn:uuid:" + Guid.NewGuid());
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
            return string.Format(ReportConstants.BundleSettings.BundlingFullUrlFormat, GetRelativeReference(resource));
        }

        /// <summary>
        /// Adds the given resource to the given bundle.
        /// If an existing resource exists with the same ID in the bundleSettings, then the provided resource will replace the existing resource.
        /// </summary>
        /// <param name="bundle"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        protected Bundle.EntryComponent AddResourceToBundle(Bundle bundle, Resource resource)
        {
            Bundle.EntryComponent entry;

            // find existing matching measure report
            var existingEntryIndex = bundle.Entry.FindIndex(e => e.Resource.TypeName == resource.TypeName && e.Resource.Id == resource.Id);
            if (existingEntryIndex < 0)
            {
                // doesn't exist... add it to the bundleSettings as-is
                entry = bundle.AddResourceEntry(resource, GetFullUrl(resource));
            }
            else
            {
                // already exists in bundleSettings... update the entry
                entry = bundle.Entry[existingEntryIndex] = new Bundle.EntryComponent() { Resource = resource, FullUrl = GetFullUrl(resource) };
            }

            return entry;
        }

        #endregion

    }

}
