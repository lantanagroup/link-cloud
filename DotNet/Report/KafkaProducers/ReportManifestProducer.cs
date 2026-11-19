using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Report.Application;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
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
        private readonly IReportScheduledManager _reportScheduleManager;
        private readonly ServiceInformation _serviceInformation;

        public ReportManifestProducer(
            ILogger<ReportManifestProducer> logger,
            IServiceScopeFactory serviceScopeFactory,
            MeasureReportAggregator aggregator,
            ITenantApiService tenantApiService,
            BlobStorageService blobStorageService,
            SubmitPayloadProducer payloadSubmittedProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer,
            IReportEntryManager reportEntryManager,
            IReportScheduledManager reportScheduleManager,
            ServiceInformation serviceInformation)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _aggregator = aggregator;
            _tenantApiService = tenantApiService;
            _blobStorageService = blobStorageService;
            _payloadSubmittedProducer = payloadSubmittedProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
            _reportEntryManager = reportEntryManager;
            _reportScheduleManager = reportScheduleManager;
            _serviceInformation = serviceInformation;
        }

        /// <summary>
        /// Builds the manifest resources from what is persisted now. Checks no gate and writes nothing.
        /// <see cref="Produce"/> calls it only once every entry is terminal. The ad-hoc manifest
        /// endpoint calls it whenever asked, so the <see cref="OperationOutcome"/> flags what is
        /// still in flight: one <c>incomplete</c> warning per non-terminal entry, plus one warning
        /// when the reporting period has not closed. Neither appears on the scheduled path.
        /// </summary>
        /// <exception cref="FacilityConfigNotFoundException">Tenant has no config for the facility.</exception>
        /// <exception cref="TenantUnavailableException">Tenant could not be asked.</exception>
        public virtual async Task<List<Resource>> Generate(ReportScheduleModel schedule, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var reportEntries = await database.ReportEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id, cancellationToken);

            var facilityConfig = await GetFacilityConfig(schedule, cancellationToken);

            var organization = FhirHelperMethods.CreateOrganization(facilityConfig.FacilityName, schedule.FacilityId, ReportConstants.BundleSettings.SubmittingOrganizationProfile, ReportConstants.BundleSettings.OrganizationTypeSystem, ReportConstants.BundleSettings.CdcOrgIdSystem, ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl, ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);

            List<Resource> manifestResources =
            [
                organization,
                CreateDevice(),
                CreatePatientList(reportEntries.Select(x => x.PatientId).Distinct().ToList(), schedule.ReportStartDate.DateTime, schedule.ReportEndDate.DateTime),
            ];

            var aggregates = await _aggregator.CreateMeasureReportAggregate(schedule, organization.Id, cancellationToken);

            foreach (var aggregate in aggregates)
            {
                manifestResources.Add(aggregate);
            }

            var operationOutcome = CreateOperationOutcome(schedule, reportEntries);

            if (operationOutcome.Issue.Count > 0)
            {
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
            return ToBundle(resources);
        }

        /// <summary>
        /// Wraps manifest resources in the <c>collection</c> Bundle <see cref="GenerateAsBundle"/> returns.
        /// </summary>
        public static Bundle ToBundle(IEnumerable<Resource> resources)
        {
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
                payloadUri = await _blobStorageService.UploadManifestAsync(schedule, manifestResources, cancellationToken);
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

            if (schedule.EnableSubmission)
            {
                _logger.LogDebug("Producing report manifest to Kafka (Facility = {FacilityId}, ReportScheduleId = {ReportScheduleId})", schedule.FacilityId.SanitizeForLog(), schedule.Id.SanitizeForLog());
                await _payloadSubmittedProducer.Produce(schedule, PayloadType.ReportSchedule,
                    payloadUri: payloadUri?.ToString());
            }
            else
            {
                schedule.Status = ScheduleStatus.CompletedNotSubmitted;
                await _reportScheduleManager.UpdateAsync(schedule, cancellationToken);
                _logger.LogDebug("Report manifest submission is disabled (Facility = {FacilityId}, ReportScheduleId = {ReportScheduleId})", schedule.FacilityId.SanitizeForLog(), schedule.Id.SanitizeForLog());
            }


            return true;
        }

        private Device CreateDevice()
        {
            var device = new Device
            {
                Meta = new Meta { Profile = [ReportConstants.BundleSettings.SubmittingDeviceProfile] }
            };
            device.DeviceName.Add(new Device.DeviceNameComponent
            {
                Name = ReportConstants.BundleSettings.DeviceName,
                Type = DeviceNameType.UserFriendlyName
            });

            var version = string.IsNullOrWhiteSpace(_serviceInformation.ProductVersion)
                ? _serviceInformation.Version
                : _serviceInformation.ProductVersion;

            AddDeviceVersion(device, ReportConstants.BundleSettings.DeviceVersionCode, version, system: null);
            AddDeviceVersion(device,
                             ReportConstants.BundleSettings.DeviceVersionBuildCode,
                             _serviceInformation.Build,
                             ReportConstants.BundleSettings.LinkDeviceVersionCodeSystem);
            AddDeviceVersion(device,
                             ReportConstants.BundleSettings.DeviceVersionCommitCode,
                             _serviceInformation.Commit,
                             ReportConstants.BundleSettings.LinkDeviceVersionCodeSystem);

            return device;
        }

        /// <summary>
        /// Adds one typed version entry in the shape MVP Link submitted. An empty value is skipped rather
        /// than written as "", which is not valid FHIR (LEGLINK-1420).
        /// </summary>
        private static void AddDeviceVersion(Device device, string code, string? value, string? system)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            device.Version.Add(new Device.VersionComponent
            {
                Type = new CodeableConcept { Coding = [new Coding { System = system, Code = code }] },
                Component = new Identifier { Value = ReportConstants.BundleSettings.DeviceVersionComponent },
                Value = value
            });
        }

        private List CreatePatientList(List<string> patientIds, DateTime startDate, DateTime endDate)
        {
            var admittedPatients = new List();
            admittedPatients.Meta = new Meta { Profile = [ReportConstants.BundleSettings.CensusProfileUrl] };
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

        private async Task<FacilityModel> GetFacilityConfig(ReportScheduleModel schedule, CancellationToken cancellationToken)
        {
            string notFound = $"Facility config was not found when attempting to generate a report manifest (ReportId = {schedule.Id}, FacilityId = {schedule.FacilityId}).";

            FacilityModel? facilityConfig;
            try
            {
                facilityConfig = await _tenantApiService.GetFacilityConfig(schedule.FacilityId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                // The Tenant client's signal for a 404.
                throw new FacilityConfigNotFoundException(notFound, ex);
            }
            catch (Exception ex)
            {
                throw new TenantUnavailableException(
                    $"Facility config could not be retrieved from Tenant when attempting to generate a report manifest (ReportId = {schedule.Id}, FacilityId = {schedule.FacilityId}).", ex);
            }

            return facilityConfig ?? throw new FacilityConfigNotFoundException(notFound);
        }

        private static OperationOutcome CreateOperationOutcome(ReportScheduleModel schedule, List<ReportEntry> reportEntries)
        {
            var operationOutcome = new OperationOutcome();

            if (!schedule.EndOfReportPeriodJobHasRun)
            {
                operationOutcome.Issue.Add(new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Warning,
                    Code = OperationOutcome.IssueType.Informational,
                    Diagnostics = "The reporting period has not closed; the patient census may still change."
                });
            }

            foreach (var entry in reportEntries.Where(x => x.ReportingStatus == ReportingStatus.FailedValidation))
            {
                operationOutcome.Issue.Add(new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.Invalid,
                    Diagnostics = $"Validation failed for patient {entry.PatientId}"
                });
            }

            foreach (var entry in reportEntries.Where(x => !ReportEntryTerminalStatuses.IsTerminal(x.ReportingStatus, x.SubmissionStatus)))
            {
                operationOutcome.Issue.Add(new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Warning,
                    Code = OperationOutcome.IssueType.Incomplete,
                    Diagnostics = $"Processing incomplete for patient {entry.PatientId} (ReportingStatus = {entry.ReportingStatus}, SubmissionStatus = {entry.SubmissionStatus?.ToString() ?? "None"})"
                });
            }

            return operationOutcome;
        }
    }
}