using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSubmission.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Queries;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using MediatR;
using System.Configuration;
using System.Linq;

namespace LantanaGroup.Link.Report.Core
{
    public class MeasureReportSubmissionBundler
    {
        private readonly ILogger<MeasureReportSubmissionBundler> _logger;
        private readonly IMediator _mediator;

        public MeasureReportSubmissionBundler(ILogger<MeasureReportSubmissionBundler> logger, IMediator mediator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
        }


        public async Task<MeasureReportSubmissionModel> GenerateBundle(string measureReportScheduleId)
        {
            if (string.IsNullOrEmpty(measureReportScheduleId))
                throw new Exception($"GenerateBundle: no measureReportScheduleId supplied");

            // find existing report scheduled for the supplied ID
            MeasureReportScheduleModel schedule = await _mediator.Send(new GetMeasureReportScheduleQuery { Id = measureReportScheduleId });
            if (schedule == null)
                throw new Exception($"No report schedule found for measureReportScheduleId {measureReportScheduleId}");

            List<MeasureReportConfigModel> configs = (await _mediator.Send(new SearchMeasureReportConfigQuery { FacilityId = schedule.FacilityId })).ToList();
            if (configs == null || configs.Count < 1)
                throw new Exception($"No report configs found for Facility {schedule.FacilityId}");


            // need a bundle object to work with... either a newly created one or by parsing the existing bundle string
            MeasureReportSubmissionModel submission = await _mediator.Send(new FindMeasureReportSubmissionByScheduleQuery { MeasureReportScheduleId = measureReportScheduleId });
            Bundle submissionBundle;
            if (submission is null)
            {
                submission = new MeasureReportSubmissionModel { MeasureReportScheduleId = measureReportScheduleId };
                submissionBundle = CreateNewBundle(schedule);
            }
            else
            {
                submissionBundle = submission.SubmissionBundle;
            }

            // ensure aggregate patient list and measure report entries are created for reach measure
            var org = submissionBundle.Entry.FirstOrDefault(e => e.Resource.TypeName == "Organization"
                && (e.Resource.Meta is not null && e.Resource.Meta.Profile is not null
                && e.Resource.Meta.Profile.Contains(ReportConstants.Bundle.SubmittingOrganizationProfile))
            );
            string orgId = org?.Resource.Id ?? "";
            foreach (var config in configs)
            {

                // aggregate patient list
                if (submissionBundle.Entry.FirstOrDefault(e => e.Resource.TypeName == "List") is null)
                {
                    var pl = CreatePatientList(new FhirDateTime(new DateTimeOffset(schedule.ReportStartDate)), new FhirDateTime(new DateTimeOffset(schedule.ReportEndDate)), config.ReportType);
                    submissionBundle.AddResourceEntry(pl, GetFullUrl(pl));
                }

                // aggregate measure report
              /*  if (GetAggregateMeasureReport(submissionBundle, config.ReportType) is null)
                {
                    var measureAgg = CreateAggregateMeasureReport(config.ReportType, orgId, new FhirDateTime(new DateTimeOffset(schedule.ReportStartDate)), new FhirDateTime(new DateTimeOffset(schedule.ReportEndDate)));
                    submissionBundle.AddResourceEntry(measureAgg, GetFullUrl(measureAgg));
                }*/
            }


            // add every fetched measure report entry to the submission bundle
            var entries = await _mediator.Send(new GetMeasureReportSubmissionEntriesQuery { MeasureReportScheduleId = measureReportScheduleId });
            var parser = new FhirJsonParser();
            foreach (var entry in entries)
            {
                // parse the MeasureReport
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

                // ensure we have an id to reference
                if (string.IsNullOrEmpty(mr.Id))
                    mr.Id = Guid.NewGuid().ToString();

                // set individual measure report profile
                mr.Meta = new Meta
                {
                    Profile = new List<string> { ReportConstants.Bundle.IndividualMeasureReportProfileUrl }
                };

                _logger.LogDebug($"Adding MeasurReport with ID [{mr.Id}] (entry mongo _id: [{entry.Id}]) to aggregate.");

                // add to aggregate measure report
                AddToAggregateMeasureReport(submissionBundle, mr, orgId);


                // bundle based on configured bundling type
                var config = configs.FirstOrDefault(c => c.ReportType == GetMeasureIdFromCanonical(mr.Measure));

                if (config != null && config.BundlingType == BundlingType.SharedPatientLineLevel)
                    BundleSharedPatientLineLevel(submissionBundle, mr);
                else
                    BundleDefault(submissionBundle, mr);

            }

            // create or update the submission bundle to storage
            submission.SubmissionBundle = submissionBundle;

            //Disabled saving the bundle to the DB
            //if (string.IsNullOrEmpty(submission.Id))
            //    await _mediator.Send(new CreateMeasureReportSubmissionCommand { MeasureReportSubmission = submission });
            //else
            //    await _mediator.Send(new UpdateMeasureReportSubmissionCommand { MeasureReportSubmission = submission });

            return submission;
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
                        bundle.Entry[existingEntryIndex] = new Bundle.EntryComponent() { Resource = resource, FullUrl = GetFullUrl(resource) };
                    }
                }

                // remove this now bundled resource from the measure report
                string id = measureReport.Contained[0].Id;
                measureReport.Contained.RemoveAt(0);
                Extension extToRemove = null;
                // remove the ext that reference the contained resource
                measureReport.Extension.ForEach(e =>
                {
                    if (e.Value is ResourceReference)
                    {
                        var s = (ResourceReference)e.Value;
                        if (s.Reference.Contains(id))
                            extToRemove = e;
                    }
                });

                if (extToRemove != null)
                    measureReport.Extension.Remove(extToRemove);
            }


        }

        #endregion




        #region Common Methods

        protected MeasureReport ReportFormater(MeasureReport measureReport)
        {
            measureReport.EvaluatedResource.ForEach(r => { if (r.Extension.Count > 0) r.Extension = new List<Extension>(); });
            return measureReport;
        }
        
        protected Bundle CreateNewBundle(MeasureReportScheduleModel reportSchedule)
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

            // add organization entry
            var org = CreateOrganization(reportSchedule.FacilityId);
            bundle.AddResourceEntry(org, GetFullUrl(org));

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
                return measureCanonical.Substring(index+1, versionIndex - index - 1);
            }
            else
            {
                // no version specified
                return measureCanonical.Substring(index+1);
            }
        }


        protected Organization CreateOrganization(String facilityId)
        {
            Organization org = new Organization();
            org.Meta = new Meta
            {
                Profile = new string[] { ReportConstants.Bundle.SubmittingOrganizationProfile }
            };
            org.Active = true;
            org.Id = Guid.NewGuid().ToString(); // or National Provider Identifier (NPI) from config?
            org.Type = new List<CodeableConcept>
            {
                new CodeableConcept(ReportConstants.Bundle.OrganizationTypeSystem, "prov", "Healthcare Provider", null)
            };

            org.Name = "EHR Test On Prem"; // should be org name from config?

            org.Identifier.Add(new Identifier
            {
                System = ReportConstants.Bundle.CdcOrgIdSystem,
                Value = facilityId // CDC org ID from config
            });

            // TODO: should phone and email be in config?
            // if phone and email not configured add data absent extension
            org.Telecom = new List<ContactPoint>
            {
                new ContactPoint
                {
                    Extension = new List<Extension>{ new Extension(ReportConstants.Bundle.DataAbsentReasonExtensionUrl, new Code(ReportConstants.Bundle.DataAbsentReasonUnknownCode) ) }
                }
            };

            // TODO: should be only if address is in config?
            // if no address configured add data absent extension
            org.Address = new List<Address>
            {
                new Address
                {
                    Extension = new List<Extension>{ new Extension(ReportConstants.Bundle.DataAbsentReasonExtensionUrl, new Code(ReportConstants.Bundle.DataAbsentReasonUnknownCode) ) }
                }
            };




            return org;
        }


        protected Bundle.EntryComponent GetAggregatePatientList(Bundle bundle, string measureCanonical)
        {
 
            return bundle.Entry.FirstOrDefault(e => e.Resource.TypeName == "List"
                && ((List)e.Resource).Identifier is not null
                && ((List)e.Resource).Identifier.Any(i => i.System == ReportConstants.Bundle.MainSystem && i.Value == GetMeasureIdFromCanonical(measureCanonical))
                && e.Resource.Meta is not null && e.Resource.Meta.Profile is not null
                && e.Resource.Meta.Profile.Contains(ReportConstants.Bundle.CensusProfileUrl)
            );

        }

        protected List CreatePatientList(FhirDateTime reportStart, FhirDateTime reportEnd, string measureCanonical)
        {
            List list = new List();
            list.Id = Guid.NewGuid().ToString();
            list.Meta = new Meta
            {
                Profile = new List<string> { ReportConstants.Bundle.CensusProfileUrl }
            };
            list.AddExtension(ReportConstants.Bundle.ApplicablePeriodExtensionUrl, new Period()
            {
                StartElement = reportStart,
                EndElement = reportEnd
            });

            list.Identifier.Add(new Identifier(ReportConstants.Bundle.MainSystem, GetMeasureIdFromCanonical(measureCanonical)));

            return list;
        }

        protected void AddPatientReferenceToList(Bundle bundle, Patient patient, string measureCanonical)
        {
            var listEntry = GetAggregatePatientList(bundle, measureCanonical);

            if (listEntry is null)
            {
                throw new Exception("Patient census list not present in the provided bundle.");
            }

            List list = (List)listEntry.Resource;

            string refVal = GetRelativeReference(patient);
            var res = list.Entry.FirstOrDefault(l => l.Item.Reference == refVal);
            if (res is null)
            {
                list.Entry.Add(new List.EntryComponent() { Item = new ResourceReference(refVal) });
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

            // ensure patient in this measure report is in the aggregate patient list
            var patient = measureReport.Contained.FirstOrDefault(c => c.TypeName == "Patient");
            if (patient is not null)
                AddPatientReferenceToList(bundle, (Patient)patient, measureReport.Measure);


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
