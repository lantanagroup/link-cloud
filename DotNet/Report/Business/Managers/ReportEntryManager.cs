using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using System.Text.Json;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportEntryManager
    {
        /// <summary>
        /// One entry with the evidence behind its mapping indicators, for the per-patient drill-down.
        /// </summary>
        Task<ReportEntryDetailModel?> GetEntryDetail(Guid reportScheduleId, string patientId,
            CancellationToken cancellationToken = default);

        Task<ReportEntryModel?> GetEntry(Guid reportScheduleId, string patientId,
            CancellationToken cancellationToken = default);

        Task<ReportEntryModel> UpdateAsync(ReportEntryModel model, CancellationToken cancellationToken);

        Task<ReportEntryModel> AddAsync(ReportEntryModel model, CancellationToken cancellationToken);

        Task AddRangeAsync(IEnumerable<ReportEntryModel> models, CancellationToken cancellationToken);

        Task<List<ReportEntryModel>> FindAsync(Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntryModel?> SingleOrDefaultAsync(Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntryModel> UpdateAsyncWithConsumerResult(MeasureReportGeneratedValue consumerValue, CancellationToken cancellationToken = default);

        Task<ReportEntryModel> UpdateAsyncWithAggregateResult(ReportEntryModel model, AggregateResult aggregateResult,
            CancellationToken cancellationToken = default);

        Task<ReportEntryModel> UpdateAsyncNotReportableEntry(ReportEntryModel model,
            CancellationToken cancellationToken = default);

        Task<PagedConfigModel<ReportEntryModel>> SearchAsync(
            string? facilityId,
            string? patientId,
            Guid? reportScheduleId,
            List<ReportingStatus>? reportingStatuses,
            List<SubmissionStatus>? submissionStatuses,
            bool submissionStatusIsNull,
            string? reportType,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken = default);

        Task<int> CountAsync(Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntrySummary> GetSummaryByReportScheduleIdAsync(Guid reportScheduleId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns <c>true</c> when every entry for the given facility/schedule
        /// has reached a terminal reporting + submission status.
        /// Uses a lightweight scalar COUNT query — no entity materialisation.
        /// </summary>
        Task<bool> AreAllEntriesCompleteAsync(string facilityId, Guid reportScheduleId,
            CancellationToken cancellationToken = default);
    }

    public class ReportEntryManager : IReportEntryManager
    {
        private readonly ReportDbContext _dbContext;
        private readonly ILogger<ReportEntryManager> _logger;

        public ReportEntryManager(ReportDbContext dbContext, ILogger<ReportEntryManager> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        private record ReportEntryProjection(
            Guid Id,
            DateTime CreateDate,
            DateTime? ModifyDate,
            string FacilityId,
            Guid ReportScheduleId,
            string PatientId,
            ReportingStatus ReportingStatus,
            SubmissionStatus? SubmissionStatus,
            DateTime? SubmitReportDateTime,
            string? AggregateReportUri,
            string? AggregateReportBlobName,
            List<MeasureReportProjection> MeasureReports,
            MappingOutcomeProjection? MappingOutcome
        );

        /// <summary>
        /// The mapping outcome row for the entry, or null when no source has reported for the patient yet.
        /// </summary>
        /// <remarks>
        /// Left-joined. An inner join would drop every patient whose outcome row does not exist -- which is
        /// every patient of every report that ran before this feature, and any patient still in flight --
        /// silently shortening the grid rather than showing them as not yet evaluated.
        /// </remarks>
        private record MappingOutcomeProjection(
            MappingIndicatorStatus LocationOrgStatus,
            MappingIndicatorStatus EncounterMappingStatus,
            MappingIndicatorStatus HslocMappingStatus,
            DateTime? AcquisitionEvaluatedAt,
            DateTime? NormalizationEvaluatedAt,
            string? AcquisitionDetails,
            string? NormalizationDetails
        );

        private record MeasureReportProjection(
            string? MeasureReportId,
            MeasureReportStatus Status,
            string ReportType,
            string? MeasureReportUri,
            string? MeasureReportFileName,
            List<ResourceCountProjection> ResourceCounts
        );

        private record ResourceCountProjection(string ResourceType, int ResourceCount);

        private static ReportEntryModel MapToModel(ReportEntryProjection proj)
        {
            return new ReportEntryModel
            {
                Id = proj.Id,
                CreateDate = proj.CreateDate,
                ModifyDate = proj.ModifyDate,
                FacilityId = proj.FacilityId,
                ReportScheduleId = proj.ReportScheduleId,
                PatientId = proj.PatientId,
                ReportingStatus = proj.ReportingStatus,
                SubmissionStatus = proj.SubmissionStatus,
                SubmitReportDateTime = proj.SubmitReportDateTime,
                AggregateReportUri = proj.AggregateReportUri,
                AggregateReportBlobName = proj.AggregateReportBlobName,
                LocationOrgStatus = proj.MappingOutcome?.LocationOrgStatus ?? MappingIndicatorStatus.NotEvaluated,
                EncounterMappingStatus = proj.MappingOutcome?.EncounterMappingStatus ?? MappingIndicatorStatus.NotEvaluated,

                // Resolved rather than copied: a patient whose encounters were all stripped as non-org
                // never reaches Normalization, so the stored NotEvaluated would never resolve on its own.
                HslocMappingStatus = MappingIndicatorView.ResolveHsloc(
                    proj.MappingOutcome?.HslocMappingStatus ?? MappingIndicatorStatus.NotEvaluated,
                    proj.MappingOutcome?.LocationOrgStatus ?? MappingIndicatorStatus.NotEvaluated,
                    proj.MappingOutcome?.NormalizationEvaluatedAt),

                AcquisitionEvaluatedAt = proj.MappingOutcome?.AcquisitionEvaluatedAt,
                NormalizationEvaluatedAt = proj.MappingOutcome?.NormalizationEvaluatedAt,
                MeasureReports = proj.MeasureReports.Select(m => new EntryMeasureReportModel
                {
                    MeasureReportId = m.MeasureReportId,
                    Status = m.Status,
                    ReportType = m.ReportType,
                    MeasureReportUri = m.MeasureReportUri,
                    MeasureReportFileName = m.MeasureReportFileName,
                    ResourceCount = m.ResourceCounts.ToDictionary(rc => rc.ResourceType, rc => rc.ResourceCount)
                }).ToList()
            };
        }

        public async Task<ReportEntryDetailModel?> GetEntryDetail(Guid reportScheduleId, string patientId,
            CancellationToken cancellationToken = default)
        {
            var entry = await GetEntry(reportScheduleId, patientId, cancellationToken);

            if (entry is null)
            {
                return null;
            }

            var stored = await _dbContext.ReportEntryMappingOutcome
                .AsNoTracking()
                .Where(o => o.ReportScheduleId == reportScheduleId && o.PatientId == patientId)
                .Select(o => new
                {
                    o.AcquisitionDetails,
                    o.AcquisitionEvaluatedAt,
                    o.NormalizationDetails,
                    o.NormalizationEvaluatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            var detail = new ReportEntryDetailModel
            {
                Id = entry.Id,
                CreateDate = entry.CreateDate,
                ModifyDate = entry.ModifyDate,
                FacilityId = entry.FacilityId,
                ReportScheduleId = entry.ReportScheduleId,
                PatientId = entry.PatientId,
                ReportingStatus = entry.ReportingStatus,
                SubmissionStatus = entry.SubmissionStatus,
                SubmitReportDateTime = entry.SubmitReportDateTime,
                AggregateReportUri = entry.AggregateReportUri,
                AggregateReportBlobName = entry.AggregateReportBlobName,
                MeasureReports = entry.MeasureReports,
                LocationOrgStatus = entry.LocationOrgStatus,
                EncounterMappingStatus = entry.EncounterMappingStatus,
                HslocMappingStatus = entry.HslocMappingStatus,
                AcquisitionEvaluatedAt = entry.AcquisitionEvaluatedAt,
                NormalizationEvaluatedAt = entry.NormalizationEvaluatedAt
            };

            // Keyed off the timestamp rather than the blob. A source that reported nothing to say still
            // stamps its timestamp, and its section should then be present-but-empty rather than absent --
            // absent means "this source has not answered", which is a different fact.
            if (stored?.AcquisitionEvaluatedAt is not null)
            {
                detail.Acquisition = Deserialize<AcquisitionMappingDetails>(
                    stored.AcquisitionDetails, reportScheduleId, patientId, "acquisition");
            }

            if (stored?.NormalizationEvaluatedAt is not null)
            {
                detail.Normalization = Deserialize<NormalizationMappingDetails>(
                    stored.NormalizationDetails, reportScheduleId, patientId, "normalization");
            }

            return detail;
        }

        /// <summary>
        /// Reads a stored detail blob into its typed model. The blob is storage, not the API contract, so
        /// a value that cannot be read is reported as absent rather than put on the wire as a string.
        /// </summary>
        private T? Deserialize<T>(string? json, Guid reportScheduleId, string patientId, string source)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Stored {Source} mapping details for report schedule {ReportScheduleId} patient {PatientId} could not be read; reporting them as absent.",
                    source, reportScheduleId, patientId);

                return null;
            }
        }

        public async Task<ReportEntryModel?> GetEntry(Guid reportScheduleId, string patientId,
            CancellationToken cancellationToken = default)
        {
            var proj = await _dbContext.ReportEntry
                .Where(e => e.ReportScheduleId == reportScheduleId && e.PatientId == patientId)
                .Select(e => new ReportEntryProjection(
                    e.Id,
                    e.CreateDate,
                    e.ModifyDate,
                    e.FacilityId,
                    e.ReportScheduleId,
                    e.PatientId,
                    e.ReportingStatus,
                    e.SubmissionStatus,
                    e.SubmitReportDateTime,
                    e.AggregateReportUri,
                    e.AggregateReportBlobName,
                    e.MeasureReports.Select(m => new MeasureReportProjection(
                        m.MeasureReportId,
                        m.Status,
                        m.ReportType,
                        m.MeasureReportUri,
                        m.MeasureReportFileName,
                        m.ResourceCounts.Select(rc => new ResourceCountProjection(rc.ResourceType, rc.ResourceCount))
                            .ToList()
                    )).ToList(),
                    _dbContext.ReportEntryMappingOutcome
                        .Where(o => o.ReportScheduleId == e.ReportScheduleId && o.PatientId == e.PatientId)
                        .Select(o => new MappingOutcomeProjection(
                            o.LocationOrgStatus,
                            o.EncounterMappingStatus,
                            o.HslocMappingStatus,
                            o.AcquisitionEvaluatedAt,
                            o.NormalizationEvaluatedAt,
                            o.AcquisitionDetails,
                            o.NormalizationDetails))
                        .FirstOrDefault()
                ))
                .FirstOrDefaultAsync(cancellationToken);

            return proj == null ? null : MapToModel(proj);
        }

        public async Task<List<ReportEntryModel>> FindAsync(Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var projList = await _dbContext.ReportEntry
                .Where(predicate)
                .Select(e => new ReportEntryProjection(
                    e.Id,
                    e.CreateDate,
                    e.ModifyDate,
                    e.FacilityId,
                    e.ReportScheduleId,
                    e.PatientId,
                    e.ReportingStatus,
                    e.SubmissionStatus,
                    e.SubmitReportDateTime,
                    e.AggregateReportUri,
                    e.AggregateReportBlobName,
                    e.MeasureReports.Select(m => new MeasureReportProjection(
                        m.MeasureReportId,
                        m.Status,
                        m.ReportType,
                        m.MeasureReportUri,
                        m.MeasureReportFileName,
                        m.ResourceCounts.Select(rc => new ResourceCountProjection(rc.ResourceType, rc.ResourceCount))
                            .ToList()
                    )).ToList(),
                    _dbContext.ReportEntryMappingOutcome
                        .Where(o => o.ReportScheduleId == e.ReportScheduleId && o.PatientId == e.PatientId)
                        .Select(o => new MappingOutcomeProjection(
                            o.LocationOrgStatus,
                            o.EncounterMappingStatus,
                            o.HslocMappingStatus,
                            o.AcquisitionEvaluatedAt,
                            o.NormalizationEvaluatedAt,
                            o.AcquisitionDetails,
                            o.NormalizationDetails))
                        .FirstOrDefault()
                ))
                .ToListAsync(cancellationToken);

            return projList.Select(MapToModel).ToList();
        }

        public async Task<ReportEntryModel?> SingleOrDefaultAsync(Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var proj = await _dbContext.ReportEntry
                .Where(predicate)
                .Select(e => new ReportEntryProjection(
                    e.Id,
                    e.CreateDate,
                    e.ModifyDate,
                    e.FacilityId,
                    e.ReportScheduleId,
                    e.PatientId,
                    e.ReportingStatus,
                    e.SubmissionStatus,
                    e.SubmitReportDateTime,
                    e.AggregateReportUri,
                    e.AggregateReportBlobName,
                    e.MeasureReports.Select(m => new MeasureReportProjection(
                        m.MeasureReportId,
                        m.Status,
                        m.ReportType,
                        m.MeasureReportUri,
                        m.MeasureReportFileName,
                        m.ResourceCounts.Select(rc => new ResourceCountProjection(rc.ResourceType, rc.ResourceCount))
                            .ToList()
                    )).ToList(),
                    _dbContext.ReportEntryMappingOutcome
                        .Where(o => o.ReportScheduleId == e.ReportScheduleId && o.PatientId == e.PatientId)
                        .Select(o => new MappingOutcomeProjection(
                            o.LocationOrgStatus,
                            o.EncounterMappingStatus,
                            o.HslocMappingStatus,
                            o.AcquisitionEvaluatedAt,
                            o.NormalizationEvaluatedAt,
                            o.AcquisitionDetails,
                            o.NormalizationDetails))
                        .FirstOrDefault()
                ))
                .SingleOrDefaultAsync(cancellationToken);

            return proj == null ? null : MapToModel(proj);
        }

        public async Task<ReportEntryModel> UpdateAsync(ReportEntryModel model, CancellationToken cancellationToken)
        {
            var entity = await _dbContext.ReportEntry
                .Include(e => e.MeasureReports)
                .ThenInclude(m => m.ResourceCounts)
                .FirstOrDefaultAsync(e => e.Id == model.Id, cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"ReportEntry with Id {model.Id} not found");

            entity.ModifyDate = DateTime.UtcNow;
            entity.AggregateReportUri = model.AggregateReportUri;
            entity.AggregateReportBlobName = model.AggregateReportBlobName;
            entity.ReportingStatus = model.ReportingStatus;
            entity.SubmissionStatus = model.SubmissionStatus;
            entity.SubmitReportDateTime = model.SubmitReportDateTime;

            var existingMeasureReports = entity.MeasureReports.ToList();

            foreach (var measureModel in model.MeasureReports)
            {
                var existing = existingMeasureReports.FirstOrDefault(m => m.ReportType == measureModel.ReportType);
                if (existing == null)
                {
                    entity.MeasureReports.Add(new EntryMeasureReport
                    {
                        ReportType = measureModel.ReportType,
                        Status = measureModel.Status,
                        MeasureReportId = measureModel.MeasureReportId,
                        MeasureReportUri = measureModel.MeasureReportUri,
                        MeasureReportFileName = measureModel.MeasureReportFileName,
                        ResourceCounts = measureModel.ResourceCount.Select(kv => new ResourceCounts
                        {
                            ResourceType = kv.Key,
                            ResourceCount = kv.Value
                        }).ToList()
                    });
                }
                else
                {
                    existing.Status = measureModel.Status;
                    existing.MeasureReportId = measureModel.MeasureReportId;
                    existing.MeasureReportUri = measureModel.MeasureReportUri;
                    existing.MeasureReportFileName = measureModel.MeasureReportFileName;

                    var existingCounts = existing.ResourceCounts.ToList();
                    foreach (var count in measureModel.ResourceCount)
                    {
                        var ec = existingCounts.FirstOrDefault(c => c.ResourceType == count.Key);
                        if (ec == null)
                        {
                            existing.ResourceCounts.Add(new ResourceCounts
                            {
                                ResourceType = count.Key,
                                ResourceCount = count.Value
                            });
                        }
                        else
                        {
                            ec.ResourceCount = count.Value;
                            existingCounts.Remove(ec);
                        }
                    }

                    foreach (var orphan in existingCounts)
                        existing.ResourceCounts.Remove(orphan);

                    existingMeasureReports.Remove(existing);
                }
            }

            foreach (var orphan in existingMeasureReports)
                entity.MeasureReports.Remove(orphan);

            _dbContext.Update(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return model;
        }

        public async Task<ReportEntryModel> AddAsync(ReportEntryModel model, CancellationToken cancellationToken)
        {
            await AddRangeAsync(new[] { model }, cancellationToken);
            return model;
        }

        public async Task AddRangeAsync(IEnumerable<ReportEntryModel> models, CancellationToken cancellationToken)
        {
            if (models == null || !models.Any())
                return;

            var entities = new List<ReportEntry>();

            foreach (var model in models)
            {
                var entity = new ReportEntry
                {
                    Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id,
                    CreateDate = DateTime.UtcNow,
                    FacilityId = model.FacilityId,
                    ReportScheduleId = model.ReportScheduleId,
                    PatientId = model.PatientId,
                    ReportingStatus = model.ReportingStatus,
                    SubmissionStatus = model.SubmissionStatus,
                    SubmitReportDateTime = model.SubmitReportDateTime,
                    AggregateReportUri = model.AggregateReportUri,
                    AggregateReportBlobName = model.AggregateReportBlobName
                };

                foreach (var measureModel in model.MeasureReports)
                {
                    var measureEntry = new EntryMeasureReport
                    {
                        ReportType = measureModel.ReportType,
                        Status = measureModel.Status,
                        MeasureReportId = measureModel.MeasureReportId,
                        MeasureReportUri = measureModel.MeasureReportUri,
                        MeasureReportFileName = measureModel.MeasureReportFileName
                    };

                    foreach (var rc in measureModel.ResourceCount)
                    {
                        measureEntry.ResourceCounts.Add(new ResourceCounts
                        {
                            ResourceType = rc.Key,
                            ResourceCount = rc.Value
                        });
                    }

                    entity.MeasureReports.Add(measureEntry);
                }

                entities.Add(entity);
            }

            await _dbContext.ReportEntry.AddRangeAsync(entities, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var entityArray = entities.ToArray();
            int index = 0;
            foreach (var model in models)
            {
                model.Id = entityArray[index++].Id;
            }
        }

        public async Task<ReportEntryModel> UpdateAsyncWithConsumerResult(MeasureReportGeneratedValue consumerValue, CancellationToken cancellationToken = default)
        {
            var scheduleId = Guid.Parse(consumerValue.ReportTrackingId);
            var model = await GetEntry(scheduleId, consumerValue.PatientId, cancellationToken);

            // It's possible to receive a MeasureReportGenerated event for a
            // (schedule, patient) combination that does not yet have a
            // ReportEntry record — for example when a patient only has a
            // Discharge event (handled by QueryDispatch) without an Admit event
            // ever being processed by the Report service's PatientEventListener.
            // In that case create a new entry rather than failing the message.
            if (model == null)
            {
                model = new ReportEntryModel
                {
                    PatientId = consumerValue.PatientId,
                    FacilityId = consumerValue.FacilityId,
                    ReportScheduleId = scheduleId,
                    ReportingStatus = ReportingStatus.PatientIdentified,
                    CreateDate = DateTime.UtcNow,
                    MeasureReports = new List<EntryMeasureReportModel>()
                };

                await AddAsync(model, cancellationToken);
            }

            var measureEntry = model.MeasureReports.FirstOrDefault(x => x.ReportType == consumerValue.ReportType);
            if (measureEntry == null)
            {
                measureEntry = new EntryMeasureReportModel
                {
                    ReportType = consumerValue.ReportType,
                    ResourceCount = new Dictionary<string, int>()
                };
                model.MeasureReports.Add(measureEntry);
            }

            measureEntry.MeasureReportId = consumerValue.MeasureReportId;
            measureEntry.MeasureReportFileName = consumerValue.MeasureReportBlobName;
            measureEntry.MeasureReportUri = consumerValue.MeasureReportURI;

            if (consumerValue.Reportable)
                measureEntry.Status = MeasureReportStatus.ReadyForValidation;
            else
                measureEntry.Status = MeasureReportStatus.NotReportable;

            return await UpdateAsync(model, cancellationToken);
        }

        public async Task<ReportEntryModel> UpdateAsyncWithAggregateResult(ReportEntryModel model,
            AggregateResult aggregateResult, CancellationToken cancellationToken = default)
        {
            model.AggregateReportUri = aggregateResult.Uri.AbsoluteUri;
            model.AggregateReportBlobName = aggregateResult.BlobName;
            model.ModifyDate = DateTime.UtcNow;

            foreach (var measureReportResult in aggregateResult.MeasureReportResults)
            {
                var measureEntry = model.MeasureReports.First(x => x.ReportType == measureReportResult.ReportType);
                measureEntry.ResourceCount = measureReportResult.ResourceCount;
            }

            return await UpdateAsync(model, cancellationToken);
        }

        public async Task<ReportEntryModel> UpdateAsyncNotReportableEntry(ReportEntryModel model,
            CancellationToken cancellationToken = default)
        {
            model.ReportingStatus = ReportingStatus.NotReportable;
            model.SubmissionStatus = SubmissionStatus.NotEligable;
            model.ModifyDate = DateTime.UtcNow;

            return await UpdateAsync(model, cancellationToken);
        }

        public async Task<int> CountAsync(Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.ReportEntry
                .Where(predicate)
                .CountAsync(cancellationToken);
        }

        public async Task<PagedConfigModel<ReportEntryModel>> SearchAsync(
            string? facilityId,
            string? patientId,
            Guid? reportScheduleId,
            List<ReportingStatus>? reportingStatuses,
            List<SubmissionStatus>? submissionStatuses,
            bool submissionStatusIsNull,
            string? reportType,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken = default)
        {
            Expression<Func<ReportEntry, bool>> predicate = x => true;

            if (!string.IsNullOrWhiteSpace(facilityId))
            {
                predicate = predicate.And(q => q.FacilityId == facilityId);
            }

            if (!string.IsNullOrWhiteSpace(patientId))
            {
                predicate = predicate.And(q => q.PatientId == patientId);
            }

            if (reportScheduleId != null && reportScheduleId != Guid.Empty)
            {
                var scheduleIsActive = await _dbContext.ReportSchedule
                    .AnyAsync(s => s.Id == reportScheduleId && s.IsDeleted != true, cancellationToken);

                if (!scheduleIsActive)
                    return new PagedConfigModel<ReportEntryModel>(new List<ReportEntryModel>(),
                        new PaginationMetadata(pageSize, pageNumber, 0));

                predicate = predicate.And(q => q.ReportScheduleId == reportScheduleId);
            }
            else
            {
                predicate = predicate.And(q =>
                    _dbContext.ReportSchedule.Any(s =>
                        s.Id == q.ReportScheduleId &&
                        s.IsDeleted != true &&
                        (string.IsNullOrWhiteSpace(facilityId) || s.FacilityId == facilityId)));
            }

            if (reportingStatuses != null && reportingStatuses.Count > 0)
            {
                predicate = predicate.And(q => reportingStatuses.Contains(q.ReportingStatus));
            }

            Expression<Func<ReportEntry, bool>>? submissionPredicate = null;
            if (submissionStatusIsNull)
            {
                submissionPredicate = x => x.SubmissionStatus == null;
            }

            if (submissionStatuses != null && submissionStatuses.Count > 0)
            {
                Expression<Func<ReportEntry, bool>> p = x =>
                    x.SubmissionStatus != null && submissionStatuses.Contains(x.SubmissionStatus.Value);
                submissionPredicate = submissionPredicate == null ? p : submissionPredicate.Or(p);
            }

            if (submissionPredicate != null)
            {
                predicate = predicate.And(submissionPredicate);
            }

            if (!string.IsNullOrWhiteSpace(reportType))
            {
                predicate = predicate.And(q => q.MeasureReports.Any(a => a.ReportType == reportType));
            }

            IQueryable<ReportEntry> entityQuery = _dbContext.ReportEntry.Where(predicate);

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                entityQuery = sortOrder == SortOrder.Descending
                    ? entityQuery.OrderByDescending(x => EF.Property<object>(x, sortBy))
                    : entityQuery.OrderBy(x => EF.Property<object>(x, sortBy));
            }
            else
            {
                entityQuery = entityQuery.OrderByDescending(x => x.CreateDate);
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                entityQuery = sortBy.ToLower() switch
                {
                    "createdate" => sortOrder == SortOrder.Descending
                        ? entityQuery.OrderByDescending(x => x.CreateDate)
                        : entityQuery.OrderBy(x => x.CreateDate),
                    "facilityid" => sortOrder == SortOrder.Descending
                        ? entityQuery.OrderByDescending(x => x.FacilityId)
                        : entityQuery.OrderBy(x => x.FacilityId),
                    "patientid" => sortOrder == SortOrder.Descending
                        ? entityQuery.OrderByDescending(x => x.PatientId)
                        : entityQuery.OrderBy(x => x.PatientId),
                    "reportingstatus" => sortOrder == SortOrder.Descending
                        ? entityQuery.OrderByDescending(x => x.ReportingStatus)
                        : entityQuery.OrderBy(x => x.ReportingStatus),
                    "submissionstatus" => sortOrder == SortOrder.Descending
                        ? entityQuery.OrderByDescending(x => x.SubmissionStatus)
                        : entityQuery.OrderBy(x => x.SubmissionStatus),
                    _ => entityQuery.OrderByDescending(x => x.CreateDate)
                };
            }
            else
            {
                entityQuery = entityQuery.OrderByDescending(x => x.CreateDate);
            }

            var query = entityQuery.Select(e => new ReportEntryProjection(
                e.Id,
                e.CreateDate,
                e.ModifyDate,
                e.FacilityId,
                e.ReportScheduleId,
                e.PatientId,
                e.ReportingStatus,
                e.SubmissionStatus,
                e.SubmitReportDateTime,
                e.AggregateReportUri,
                e.AggregateReportBlobName,
                e.MeasureReports.Select(m => new MeasureReportProjection(
                    m.MeasureReportId,
                    m.Status,
                    m.ReportType,
                    m.MeasureReportUri,
                    m.MeasureReportFileName,
                    m.ResourceCounts.Select(rc => new ResourceCountProjection(rc.ResourceType, rc.ResourceCount))
                        .ToList()
                )).ToList(),
                _dbContext.ReportEntryMappingOutcome
                    .Where(o => o.ReportScheduleId == e.ReportScheduleId && o.PatientId == e.PatientId)
                    .Select(o => new MappingOutcomeProjection(
                        o.LocationOrgStatus,
                        o.EncounterMappingStatus,
                        o.HslocMappingStatus,
                        o.AcquisitionEvaluatedAt,
                        o.NormalizationEvaluatedAt,
                        o.AcquisitionDetails,
                        o.NormalizationDetails))
                    .FirstOrDefault()
            ));

            var projList = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var results = projList.Select(MapToModel).ToList();

            var totalCount = await _dbContext.ReportEntry
                .Where(predicate)
                .LongCountAsync(cancellationToken);

            return new PagedConfigModel<ReportEntryModel>(results,
                new PaginationMetadata(pageSize, pageNumber, totalCount));
        }

        public async Task<ReportEntrySummary> GetSummaryByReportScheduleIdAsync(Guid reportScheduleId,
            CancellationToken cancellationToken = default)
        {
            var entries = await _dbContext.ReportEntry
                .Where(x => x.ReportScheduleId == reportScheduleId)
                .Select(e => new { e.ReportingStatus, e.SubmissionStatus, e.MeasureReports })
                .ToListAsync(cancellationToken);

            var summary = new ReportEntrySummary();

            foreach (var entry in entries)
            {
                var reportingStatusKey = entry.ReportingStatus.ToString();
                if (summary.ReportingStatusCounts.ContainsKey(reportingStatusKey))
                    summary.ReportingStatusCounts[reportingStatusKey] = summary.ReportingStatusCounts.GetValueOrDefault(reportingStatusKey) + 1;
                else
                    summary.ReportingStatusCounts[reportingStatusKey] = 1;

                var submissionStatusKey = entry.SubmissionStatus.HasValue
                    ? entry.SubmissionStatus.Value.ToString()
                    : "Pending";

                if (summary.SubmissionStatusCounts.ContainsKey(submissionStatusKey))
                    summary.SubmissionStatusCounts[submissionStatusKey] = summary.SubmissionStatusCounts.GetValueOrDefault(submissionStatusKey) + 1;
                else
                    summary.SubmissionStatusCounts[submissionStatusKey] = 1;

                if (entry.SubmissionStatus != SubmissionStatus.NotEligable)
                {
                    foreach (var measureReport in entry.MeasureReports)
                    {
                        if (summary.ReportTypeCounts.ContainsKey(measureReport.ReportType))
                            summary.ReportTypeCounts[measureReport.ReportType] = summary.ReportTypeCounts.GetValueOrDefault(measureReport.ReportType) + 1;
                        else
                            summary.ReportTypeCounts[measureReport.ReportType] = 1;
                    }
                }
            }

            return summary;
        }

        private static readonly ReportingStatus[] TerminalReportingStatuses =
        [
            ReportingStatus.NotReportable,
            ReportingStatus.PassedValidation,
            ReportingStatus.FailedValidation
        ];

        private static readonly SubmissionStatus[] TerminalSubmissionStatuses =
        [
            SubmissionStatus.Submitted,
            SubmissionStatus.NotEligable
        ];

        public async Task<bool> AreAllEntriesCompleteAsync(string facilityId, Guid reportScheduleId,
            CancellationToken cancellationToken = default)
        {
            var incompleteCount = await _dbContext.ReportEntry
                .Where(e => e.FacilityId == facilityId
                         && e.ReportScheduleId == reportScheduleId
                         && !(
                             TerminalReportingStatuses.Contains(e.ReportingStatus)
                             && e.SubmissionStatus != null
                             && TerminalSubmissionStatuses.Contains(e.SubmissionStatus.Value)
                         ))
                .CountAsync(cancellationToken);

            return incompleteCount == 0;
        }
    }
}