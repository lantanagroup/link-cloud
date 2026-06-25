using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class ReportManifestProducer
    {
        private readonly ILogger<ReportManifestProducer> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly MeasureReportAggregator _aggregator;
        private readonly ITenantApiService _tenantApiService;
        private readonly BlobStorageService _blobStorageService;
        private readonly SubmitPayloadProducer _payloadSubmittedProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;
        private readonly IReportEntryManager _reportEntryManager;


        public ReportManifestProducer(
            ILogger<ReportManifestProducer> logger,
            IServiceScopeFactory serviceScopeFactory,
            MeasureReportAggregator aggregator,
            ITenantApiService tenantApiService,
            BlobStorageService blobStorageService,
            SubmitPayloadProducer payloadSubmittedProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer,
            IReportEntryManager reportEntryManager)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _aggregator = aggregator;
            _tenantApiService = tenantApiService;
            _blobStorageService = blobStorageService;
            _payloadSubmittedProducer = payloadSubmittedProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
            _reportEntryManager = reportEntryManager;
        }

        public virtual async Task<List<Resource>> Generate(ReportScheduleModel schedule, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var reportEntries = await database.ReportEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id);

            var facilityConfig = await _tenantApiService.GetFacilityConfig(schedule.FacilityId, cancellationToken);

            if (facilityConfig == null)
            {
                throw new Exception($"Facility config was not found when attempting to generate a report manifest (ReportId = {schedule.Id}, FacilityId = {schedule.FacilityId});");
            }

            var organization = FhirHelperMethods.CreateOrganization(facilityConfig.FacilityName, schedule.FacilityId, ReportConstants.BundleSettings.SubmittingOrganizationProfile, ReportConstants.BundleSettings.OrganizationTypeSystem, ReportConstants.BundleSettings.CdcOrgIdSystem, ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl, ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);

            List<Resource> manifestResources =
            [
                organization,
                CreateDevice(),
                CreatePatientList(reportEntries.Select(x => x.PatientId).ToList(), schedule.ReportStartDate.DateTime, schedule.ReportEndDate.DateTime),
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

        public virtual async Task<Bundle> GenerateAsBundle(ReportScheduleModel schedule, CancellationToken cancellationToken = default)
        {
            List<Resource> resources = await Generate(schedule, cancellationToken);
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

        public virtual async Task<bool> Produce(ReportScheduleModel schedule, string correlationId = null, CancellationToken cancellationToken = default)
        {
            if (!schedule.EndOfReportPeriodJobHasRun)
            {
                return false;
            }

            if (!await _reportEntryManager.AreAllEntriesCompleteAsync(schedule.FacilityId, schedule.Id, cancellationToken))
            {
                return false;
            }

            List<Resource> manifestResources = await Generate(schedule, cancellationToken);

            Uri? payloadUri;
            try
            {
                payloadUri = await _blobStorageService.UploadManifestAsync(schedule, manifestResources);
            }
            catch (Exception ex)
            {
                payloadUri = null;
                _logger.LogError(ex, "Failed to upload report manifest to blob storage (ReportId = {ReportId}, FacilityId = {FacilityId}).", schedule.Id.SanitizeForLog(), schedule.FacilityId.SanitizeForLog());
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

            _logger.LogDebug("Manifest generated (Facility = {FacilityId}, ReportScheduleId = {ReportScheduleId})", schedule.FacilityId.SanitizeForLog(), schedule.Id.SanitizeForLog());

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

        private List CreatePatientList(List<string> patientIds, DateTime startDate, DateTime endDate)
        {
            var admittedPatients = new List();
            admittedPatients.Status = List.ListStatus.Current;
            admittedPatients.Mode = ListMode.Snapshot;
            admittedPatients.Extension.Add(new Extension()
            {
                Url = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension",
                Value = new Period()
                {
                    StartElement = new FhirDateTime(new DateTimeOffset(startDate)),
                    EndElement = new FhirDateTime(new DateTimeOffset(endDate))
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