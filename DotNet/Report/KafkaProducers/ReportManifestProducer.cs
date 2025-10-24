using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class ReportManifestProducer
    {
        private readonly ILogger<ReportManifestProducer> _logger;
        private readonly IDatabase _database;
        private readonly MeasureReportAggregator _aggregator;
        private readonly ITenantApiService _tenantApiService;
        private readonly BlobStorageService _blobStorageService;
        private readonly SubmitPayloadProducer _payloadSubmittedProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;

        public ReportManifestProducer(
            ILogger<ReportManifestProducer> logger,
            IDatabase database,
            MeasureReportAggregator aggregator,
            ITenantApiService tenantApiService,
            BlobStorageService blobStorageService,
            SubmitPayloadProducer payloadSubmittedProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer)
        {
            _logger = logger;
            _database = database;
            _aggregator = aggregator;
            _tenantApiService = tenantApiService;
            _blobStorageService = blobStorageService;
            _payloadSubmittedProducer = payloadSubmittedProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
        }

        public async Task<List<Resource>> Generate(ReportScheduleModel schedule)
        {
            var allSubmissionEntries = await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id);

            var submissionEntries = allSubmissionEntries.Where(x => x.Status != PatientSubmissionStatus.NotReportable).ToList();

            var measureReports = submissionEntries
                        .Select(e => e.MeasureReport)
                        .Where(report => report != null).ToList();

            var allPatientIds = allSubmissionEntries.Select(s => s.PatientId).Distinct().ToList();

            var patientIds = submissionEntries.Where(s => s.Status == PatientSubmissionStatus.ValidationComplete || s.Status == PatientSubmissionStatus.Submitted).Select(s => s.PatientId).Distinct().ToList();

            var failedEntries = submissionEntries.Where(s => s.ValidationStatus == ValidationStatus.Failed).ToList();

            var facilityConfig = await _tenantApiService.GetFacilityConfig(schedule.FacilityId, CancellationToken.None);

            var organization = FhirHelperMethods.CreateOrganization(facilityConfig.FacilityName, schedule.FacilityId, ReportConstants.BundleSettings.SubmittingOrganizationProfile, ReportConstants.BundleSettings.OrganizationTypeSystem,
                                                                            ReportConstants.BundleSettings.CdcOrgIdSystem, ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl, ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);

            var aggregates = _aggregator.Aggregate(measureReports, organization.Id, schedule.ReportStartDate, schedule.ReportEndDate);

            var measureIds = measureReports.Select(mr => mr.Measure).Distinct().ToList();

            var reportName = _blobStorageService.GetReportName(schedule);

            var patientFileDict = patientIds.ToDictionary(pid => pid, pid => $"{reportName}_{pid}.ndjson");

            List<Resource> manifestResources =
            [
                organization,
                CreateDevice(),
                CreatePatientList(allPatientIds, schedule.ReportStartDate, schedule.ReportEndDate),
            ];

            foreach (var aggregate in aggregates)
            {
                AddExtensionsToAggregate(aggregate, patientFileDict);
                manifestResources.Add(aggregate);
            }

            var operationOutcome = CreateOperationOutcome(failedEntries);
            if (operationOutcome.Issue.Any())
            {
                manifestResources.Add(operationOutcome);
            }

            foreach (var resource in manifestResources)
            {
                resource.Id ??= Guid.NewGuid().ToString();
            }

            return manifestResources;
        }

        public async Task<Bundle> GenerateAsBundle(ReportScheduleModel schedule)
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

        public async Task<bool> Produce(ReportScheduleModel schedule, string correlationId = null)
        {
            var allReady = !await _database.SubmissionEntryRepository.AnyAsync(e => e.FacilityId == schedule.FacilityId
                && e.ReportScheduleId == schedule.Id
                && e.Status != PatientSubmissionStatus.NotReportable
                && e.Status != PatientSubmissionStatus.ValidationComplete
                && e.Status != PatientSubmissionStatus.Submitted, CancellationToken.None);

            if (!allReady)
            {
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
                _logger.LogError(ex, "Failed to upload to blob storage.");
                AuditEventMessage auditEvent = new()
                {
                    FacilityId = schedule.FacilityId,
                    CorrelationId = correlationId,
                    EventDate = DateTime.UtcNow,
                    Notes = $"Failed to upload to blob storage: {ex}"
                };
                await _auditableEventOccurredProducer.ProduceAsync(auditEvent);
            }

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

        private void AddExtensionsToAggregate(MeasureReport measureReport, Dictionary<string, string> patientFileDict)
        {
            foreach (var list in measureReport.Contained.OfType<List>())
            {
                foreach (var entry in list.Entry)
                {
                    string? patRef = entry.Item?.Reference;
                    if (string.IsNullOrEmpty(patRef)) continue;

                    string patId = patRef.Replace("Patient/", "");
                    if (patientFileDict.TryGetValue(patId, out var filename))
                    {
                        entry.AddExtension("https://measures.nhsnlink.org/StructureDefinition/link-file-reference-extension", new FhirString(filename));
                    }
                }
            }
        }

        private OperationOutcome CreateOperationOutcome(List<MeasureReportSubmissionEntryModel> failedEntries)
        {
            var operationOutcome = new OperationOutcome();
            foreach (var entry in failedEntries)
            {
                // Assuming PatientSubmissionEntry has a ValidationMessage property; adjust as per actual model
                operationOutcome.Issue.Add(new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Fatal,
                    Code = OperationOutcome.IssueType.Invalid,
                    Diagnostics = $"Validation failed for patient {entry.PatientId}"
                });
            }
            return operationOutcome;
        }
    }
}