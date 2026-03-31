using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class ReportManifestProducer
    {
        private readonly ILogger<ReportManifestProducer> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly MeasureReportAggregator _aggregator;
        private readonly IFacilityServiceClient _facilityClient;
        private readonly BlobStorageService _blobStorageService;
        private readonly SubmitPayloadProducer _payloadSubmittedProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;


        public ReportManifestProducer(
            ILogger<ReportManifestProducer> logger,
            IServiceScopeFactory serviceScopeFactory,
            MeasureReportAggregator aggregator,
            IFacilityServiceClient facilityClient,
            BlobStorageService blobStorageService,
            SubmitPayloadProducer payloadSubmittedProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _aggregator = aggregator;
            _facilityClient = facilityClient;
            _blobStorageService = blobStorageService;
            _payloadSubmittedProducer = payloadSubmittedProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
        }

        public virtual async Task<List<Resource>> Generate(ReportScheduleModel schedule)
        {
            var database = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();
            var reportEntries = await database.ReportEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id);

            FacilityModel? facilityConfig = await _facilityClient.GetAsync(schedule.FacilityId, CancellationToken.None);

            if (facilityConfig == null)
            {
                throw new Exception($"Facility config was not found when attempting to generate a report manifest (ReportId = {schedule.Id}, FacilityId = {schedule.FacilityId});");
            }

            var organization = FhirHelperMethods.CreateOrganization(facilityConfig.FacilityName, schedule.FacilityId, ReportConstants.BundleSettings.SubmittingOrganizationProfile, ReportConstants.BundleSettings.OrganizationTypeSystem, ReportConstants.BundleSettings.CdcOrgIdSystem, ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl, ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);

            List<Resource> manifestResources =
            [
                organization,
                CreateDevice(),
                CreatePatientList(reportEntries.Select(x => x.PatientId).ToList(), schedule.ReportStartDate, schedule.ReportEndDate),
            ];

            var reportName = _blobStorageService.GetReportName(schedule);
            var submittedPatientIds = reportEntries.Where(x => x.SubmissionStatus == SubmissionStatus.Submitted).Select(x => x.PatientId).ToList();
            var patientFileDict = submittedPatientIds.ToDictionary(pid => pid, pid => $"{reportName}_{pid}.ndjson");
            var aggregates = await _aggregator.CreateMeasureReportAggregate(schedule, organization.Id);

            foreach (var aggregate in aggregates)
            {
                manifestResources.Add(aggregate);
            }

            var failedEntries = reportEntries.Where(x => x.ReportingStatus == ReportingStatus.FailedValidation).ToList();

            if (failedEntries.Count > 0)
            {
                var operationOutcome = CreateOperationOutcome(failedEntries);
                manifestResources.Add(operationOutcome);
            }

            foreach (var resource in manifestResources)
            {
                resource.Id ??= Guid.NewGuid().ToString();
            }

            return manifestResources;
        }

        public virtual async Task<Bundle> GenerateAsBundle(ReportScheduleModel schedule)
        {
            List<Resource> resources = await Generate(schedule);
            Bundle bundle = new()
            {
                Type = Bundle.BundleType.Collection
            };
            Uri baseUrl = new(ReportConstants.BundleSettings.BundlingUrlBase);
            foreach (var resource in resources)
            {
                ResourceIdentity identity = ResourceIdentity.Build(baseUrl, resource.TypeName, resource.Id);
                bundle.AddResourceEntry(resource, identity.AbsoluteUri);
            }
            return bundle;
        }

        public virtual async Task<bool> Produce(ReportScheduleModel schedule, string correlationId = null)
        {
            if (!schedule.EndOfReportPeriodJobHasRun)
            {
                return false;
            }

            var database = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();

            var reportEntries = await database.ReportEntryRepository.FindAsync(x => x.FacilityId == schedule.FacilityId && x.ReportScheduleId == schedule.Id);

            foreach (var entry in reportEntries)
            {
                if ((entry.ReportingStatus == ReportingStatus.NotReportable || entry.ReportingStatus == ReportingStatus.PassedValidation || entry.ReportingStatus == ReportingStatus.FailedValidation) && (entry.SubmissionStatus == SubmissionStatus.Submitted || entry.SubmissionStatus == SubmissionStatus.NotEligable))
                {
                    continue;
                }

                return false;
            }

            List<Resource> manifestResources = await Generate(schedule);

            Uri? payloadUri;
            try
            {
                payloadUri = await _blobStorageService.UploadManifestAsync(schedule, manifestResources);
            }
            catch (Exception ex)
            {
                payloadUri = null;
                _logger.LogError(ex, "Failed to upload report manifest to blob storage (ReportId = {ReportId}, FacilityId = {FacilityId}).", schedule.Id, schedule.FacilityId.SanitizeUntrustedString());
                AuditEventMessage auditEvent = new()
                {
                    FacilityId = schedule.FacilityId,
                    CorrelationId = correlationId,
                    EventDate = DateTime.UtcNow,
                    Notes = $"Failed to upload to blob storage: {ex}"
                };
                await _auditableEventOccurredProducer.ProduceAsync(auditEvent);

                // Return false to indicate failure
                return false;
            }

            _logger.LogDebug("Manifest generated (Facility = {FacilityId}, ReportScheduleId = {ReportScheduleId})", schedule.FacilityId, schedule.Id);

            await _payloadSubmittedProducer.Produce(schedule, PayloadType.ReportSchedule, payloadUri: payloadUri?.ToString());

            return true;
        }

        private Device CreateDevice()
        {
            var device = new Device();
            device.DeviceName.Add(new Device.DeviceNameComponent()
            {
                Name = "NHSNLink"
            });

            string? version = ServiceActivitySource.ProductVersion;

            if (string.IsNullOrEmpty(version))
                version = ServiceActivitySource.Instance.Version ?? "unknown";

            device.Version.Add(new Device.VersionComponent
            {
                Value = version
            });

            return device;
        }

        private List CreatePatientList(List<string> patientIds, DateTimeOffset startDate, DateTimeOffset endDate)
        {
            var admittedPatients = new List();
            admittedPatients.Status = List.ListStatus.Current;
            admittedPatients.Mode = ListMode.Snapshot;
            admittedPatients.Extension.Add(new Extension()
            {
                Url = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension",
                Value = new Period()
                {
                    StartElement = new FhirDateTime(startDate),
                    EndElement = new FhirDateTime(endDate)
                }
            });

            foreach (var patient in patientIds)
            {
                string reference = patient.StartsWith("Patient/") ? patient : "Patient/" + patient;
                admittedPatients.Entry.Add(new List.EntryComponent()
                {
                    Item = new ResourceReference(reference)
                });
            }

            return admittedPatients;
        }

        private OperationOutcome CreateOperationOutcome(List<ReportEntry> failedEntries)
        {
            var operationOutcome = new OperationOutcome();
            foreach (var entry in failedEntries)
            {
                // Assuming PatientSubmissionEntry has a ValidationMessage property; adjust as per actual model
                operationOutcome.Issue.Add(new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.Invalid,
                    Diagnostics = $"Validation failed for patient {entry.PatientId}"
                });
            }
            return operationOutcome;
        }
    }
}