using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using Link.UI.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Link.UI.Services;

/// <summary>
/// Reads slim rows from Automation.UI's <c>automation_runs</c> collection.
/// Run configuration JSON and snapshots stay in that database and are not loaded.
/// </summary>
public sealed class AutomationRunReader
{
    public const string NotConfiguredMessage =
        "Automation storage is not configured. Set MongoDB:ConnectionString and MongoDB:DatabaseName to the database Automation.UI writes.";

    public const string SettingsRejectedMessage =
        "Automation storage settings could not be read. Check MongoDB:ConnectionString and MongoDB:DatabaseName.";

    public const string UnreachableMessage =
        "Automation storage is not reachable. Stored runs will appear here when that database answers.";

    private const string CollectionName = "automation_runs";

    private static readonly ProjectionDefinition<OwnershipRunDocument> OwnershipProjection =
        Builders<OwnershipRunDocument>.Projection
            .Include(row => row.RunId)
            .Include(row => row.FacilityId)
            .Include(row => row.AutomationCreatedFacility)
            .Include(row => row.CreatedAt);

    private static readonly ProjectionDefinition<OwnershipTombstoneDocument> TombstoneProjection =
        Builders<OwnershipTombstoneDocument>.Projection
            .Include(row => row.FacilityId)
            .Include(row => row.RunId)
            .Include(row => row.CreatedAt);

    private static readonly ProjectionDefinition<AutomationRunDocument> SummaryProjection =
        Builders<AutomationRunDocument>.Projection
            .Include(row => row.RunId)
            .Include(row => row.FacilityId)
            .Include(row => row.AutomationCreatedFacility)
            .Include(row => row.ReportId)
            .Include(row => row.RunName)
            .Include(row => row.Scenario)
            .Include(row => row.Status)
            .Include(row => row.PatientCount)
            .Include(row => row.Seed)
            .Include(row => row.IsMetricsRun)
            .Include(row => row.CreatedAt)
            .Include(row => row.IsActive)
            .Include(row => row.StartedAt)
            .Include(row => row.FinishedAt)
            .Include(row => row.Error)
            .Include(row => row.Duration)
            .Include(row => row.GeneratedTemplateCacheVersionNumber);

    private readonly IMongoCollection<AutomationRunDocument>? _runs;
    private readonly ILogger<AutomationRunReader> _logger;
    private readonly string? _unavailableMessage;

    private AutomationRunReader(
        IMongoCollection<AutomationRunDocument>? runs,
        bool liveConfigured,
        string? unavailableMessage,
        ILogger<AutomationRunReader> logger)
    {
        _runs = runs;
        LiveConfigured = liveConfigured;
        _unavailableMessage = unavailableMessage;
        _logger = logger;
    }

    public bool Configured => _runs is not null;

    public bool LiveConfigured { get; }

    public static AutomationRunReader Create(IConfiguration configuration, ILogger<AutomationRunReader> logger)
    {
        var connectionString = configuration["MongoDB:ConnectionString"];
        var databaseName = configuration["MongoDB:DatabaseName"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
            return new AutomationRunReader(null, liveConfigured: true, NotConfiguredMessage, logger);

        try
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(3);
            var database = new MongoClient(settings).GetDatabase(databaseName.Trim());
            return new AutomationRunReader(
                database.GetCollection<AutomationRunDocument>(CollectionName),
                liveConfigured: true,
                null,
                logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Automation storage settings could not be read ({ExceptionType}).",
                ex.GetType().Name);
            return new AutomationRunReader(null, liveConfigured: true, SettingsRejectedMessage, logger);
        }
    }

    public async Task<AutomationDashboardPage> LoadDashboardAsync(
        AutomationRunQuery query,
        CancellationToken cancellationToken)
    {
        var pageNumber = AutomationRules.NormalizePageNumber(query.PageNumber);
        var pageSize = AutomationRules.NormalizePageSize(query.PageSize);
        var sortBy = AutomationRules.NormalizeSortBy(query.SortBy);
        var descending = AutomationRules.IsDescending(query.SortDir);

        if (_runs is null)
        {
            return EmptyDashboard(pageNumber, pageSize, sortBy, descending, configured: false, reachable: false, _unavailableMessage);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var token = timeout.Token;
            var now = DateTimeOffset.UtcNow;
            var since = now.AddDays(-AutomationRules.WindowDays);

            var windowRows = await FindRowsAsync(
                Builders<AutomationRunDocument>.Filter.Gte(row => row.CreatedAt, since),
                Builders<AutomationRunDocument>.Sort.Descending(row => row.CreatedAt),
                skip: 0,
                limit: null,
                token);

            var total = await _runs.CountDocumentsAsync(
                FilterDefinition<AutomationRunDocument>.Empty,
                cancellationToken: token);

            var recent = await FindRowsAsync(
                FilterDefinition<AutomationRunDocument>.Empty,
                Sort(sortBy, descending),
                (pageNumber - 1) * pageSize,
                pageSize,
                token);

            var active = await LoadActiveAsync(token);
            var scenarios = await ListScenariosAsync(token);

            return new AutomationDashboardPage
            {
                StorageConfigured = true,
                StorageReachable = true,
                LiveConfigured = LiveConfigured,
                Stats = AutomationRules.BuildStats(windowRows, now),
                ActiveRuns = active,
                RecentRuns = recent,
                Scenarios = scenarios.Scenarios,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = AutomationRules.TotalPages(total, pageSize),
                SortBy = sortBy,
                SortDir = AutomationRules.SortDir(descending)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EmptyDashboard(pageNumber, pageSize, sortBy, descending, configured: true, reachable: false, UnreachableMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Automation run storage could not be read ({ExceptionType}).",
                ex.GetType().Name);
            return EmptyDashboard(pageNumber, pageSize, sortBy, descending, configured: true, reachable: false, UnreachableMessage);
        }
    }

    /// <summary>
    /// Active runs plus the newest rows for the home page. Skips the 14-day chart scan and the scenario list.
    /// </summary>
    public async Task<HomeRunSlice> LoadHomeSliceAsync(int take, CancellationToken cancellationToken)
    {
        var limit = take < 1 ? 1 : Math.Min(take, 10);
        if (_runs is null)
            return HomeRunSlice.Unavailable(_unavailableMessage ?? NotConfiguredMessage);

        try
        {
            var active = await LoadActiveAsync(cancellationToken);
            var recent = await FindRowsAsync(
                FilterDefinition<AutomationRunDocument>.Empty,
                Builders<AutomationRunDocument>.Sort.Descending(row => row.CreatedAt),
                skip: 0,
                Math.Min(limit * 2, 10),
                cancellationToken);
            return new HomeRunSlice
            {
                Reachable = true,
                ActiveCount = active.Count,
                Active = active.Take(limit).ToList(),
                Recent = recent
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HomeRunSlice.Unavailable(UnreachableMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Automation run storage could not be read ({ExceptionType}).",
                ex.GetType().Name);
            return HomeRunSlice.Unavailable(UnreachableMessage);
        }
    }

    public async Task<AutomationRunPage> LoadRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        if (_runs is null)
        {
            return new AutomationRunPage
            {
                Found = false,
                StorageConfigured = false,
                StorageReachable = false,
                Message = _unavailableMessage,
                LiveConfigured = LiveConfigured,
                RequestedId = runId
            };
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var rows = await FindRowsAsync(
                Builders<AutomationRunDocument>.Filter.Eq(row => row.RunId, runId),
                Builders<AutomationRunDocument>.Sort.Descending(row => row.CreatedAt),
                skip: 0,
                limit: 1,
                timeout.Token);

            var run = rows.FirstOrDefault();
            return new AutomationRunPage
            {
                Found = run is not null,
                StorageConfigured = true,
                StorageReachable = true,
                Message = run is null ? "This run is not in Automation storage." : null,
                LiveConfigured = LiveConfigured,
                RequestedId = runId,
                Run = run
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UnreachableRun(runId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Automation run {RunId} could not be read ({ExceptionType}).",
                runId,
                ex.GetType().Name);
            return UnreachableRun(runId);
        }
    }

    public async Task<AutomationScenarioList> ListScenariosAsync(CancellationToken cancellationToken)
    {
        if (_runs is null)
        {
            return new AutomationScenarioList
            {
                StorageConfigured = false,
                StorageReachable = false,
                Message = _unavailableMessage
            };
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var collection = _runs.Database.GetCollection<ScenarioChoiceDocument>("automation_scenarios");
            var projection = Builders<ScenarioChoiceDocument>.Projection
                .Include(row => row.Id)
                .Include(row => row.Name)
                .Include(row => row.Description)
                .Include(row => row.ReportMethod)
                .Include(row => row.SelectedMeasures)
                .Include(row => row.UpdatedAt)
                .Include(row => row.IsSystemScenario);
            var documents = await collection
                .Find(FilterDefinition<ScenarioChoiceDocument>.Empty, new FindOptions { MaxTime = TimeSpan.FromSeconds(8) })
                .Project<ScenarioChoiceDocument>(projection)
                .Limit(LinkAutomationStartRules.ScenarioListLimit)
                .ToListAsync(timeout.Token);

            return new AutomationScenarioList
            {
                StorageConfigured = true,
                StorageReachable = true,
                Truncated = documents.Count >= LinkAutomationStartRules.ScenarioListLimit,
                Scenarios = documents
                    .Select(document => new AutomationScenarioChoice
                    {
                        Id = document.Id,
                        Name = document.Name ?? string.Empty,
                        Description = document.Description ?? string.Empty,
                        ReportMethod = document.ReportMethod.ToString(),
                        Measures = string.Join(" ", (document.SelectedMeasures ?? []).Select(ProfiledMeasureCatalog.GetDisplayName)),
                        UpdatedAtUnixMs = document.UpdatedAt.ToUnixTimeMilliseconds(),
                        IsSystemScenario = document.IsSystemScenario
                    })
                    .ToList()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UnreachableScenarios();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Automation scenarios could not be listed ({ExceptionType}).",
                ex.GetType().Name);
            return UnreachableScenarios();
        }
    }

    private AutomationScenarioList UnreachableScenarios() =>
        new()
        {
            StorageConfigured = true,
            StorageReachable = false,
            Message = UnreachableMessage
        };

    private AutomationRunPage UnreachableRun(Guid runId) =>
        new()
        {
            Found = false,
            StorageConfigured = true,
            StorageReachable = false,
            Message = UnreachableMessage,
            LiveConfigured = LiveConfigured,
            RequestedId = runId
        };

    private AutomationDashboardPage EmptyDashboard(
        int pageNumber,
        int pageSize,
        string sortBy,
        bool descending,
        bool configured,
        bool reachable,
        string? message) =>
        new()
        {
            StorageConfigured = configured,
            StorageReachable = reachable,
            Message = message,
            LiveConfigured = LiveConfigured,
            Stats = AutomationRules.BuildStats([], DateTimeOffset.UtcNow),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = 1,
            SortBy = sortBy,
            SortDir = AutomationRules.SortDir(descending)
        };

    public async Task<AutomationOwnershipLoad> LoadOwnershipAsync(CancellationToken cancellationToken)
    {
        if (_runs is null)
            return new AutomationOwnershipLoad();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var token = timeout.Token;
            var runs = await _runs.Database
                .GetCollection<OwnershipRunDocument>(CollectionName)
                .Find(FilterDefinition<OwnershipRunDocument>.Empty, new FindOptions { MaxTime = TimeSpan.FromSeconds(8) })
                .Project<OwnershipRunDocument>(OwnershipProjection)
                .ToListAsync(token);
            var tombstones = await _runs.Database
                .GetCollection<OwnershipTombstoneDocument>("automation_owned_facility_tombstones")
                .Find(FilterDefinition<OwnershipTombstoneDocument>.Empty, new FindOptions { MaxTime = TimeSpan.FromSeconds(8) })
                .Project<OwnershipTombstoneDocument>(TombstoneProjection)
                .ToListAsync(token);

            return new AutomationOwnershipLoad
            {
                Reachable = true,
                Runs = runs
                    .Select(row => new AutomationRunMark(row.RunId, row.FacilityId, row.AutomationCreatedFacility, row.CreatedAt))
                    .ToList(),
                Tombstones = tombstones
                    .Select(row => new AutomationTombstoneMark(row.FacilityId, row.RunId, row.CreatedAt))
                    .ToList()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Automation ownership could not be read (timeout).");
            return new AutomationOwnershipLoad();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Automation ownership could not be read ({ExceptionType}).",
                ex.GetType().Name);
            return new AutomationOwnershipLoad();
        }
    }

    private async Task<IReadOnlyList<AutomationRunRow>> LoadActiveAsync(CancellationToken cancellationToken)
    {
        var byFlag = await FindRowsAsync(
            Builders<AutomationRunDocument>.Filter.Eq(row => row.IsActive, true),
            Builders<AutomationRunDocument>.Sort.Descending(row => row.CreatedAt),
            skip: 0,
            limit: 100,
            cancellationToken);

        var byStatus = await FindRowsAsync(
            Builders<AutomationRunDocument>.Filter.In(row => row.Status, AutomationRules.ActiveStatuses),
            Builders<AutomationRunDocument>.Sort.Descending(row => row.CreatedAt),
            skip: 0,
            limit: 100,
            cancellationToken);

        return byFlag
            .Concat(byStatus)
            .Where(row => AutomationRules.IsActiveCard(row.Status))
            .GroupBy(row => row.RunId)
            .Select(group => group.First())
            .OrderByDescending(row => row.CreatedAt)
            .Take(24)
            .ToList();
    }

    private async Task<List<AutomationRunRow>> FindRowsAsync(
        FilterDefinition<AutomationRunDocument> filter,
        SortDefinition<AutomationRunDocument> sort,
        int skip,
        int? limit,
        CancellationToken cancellationToken)
    {
        var find = _runs!.Find(filter, new FindOptions { MaxTime = TimeSpan.FromSeconds(8) })
            .Project<AutomationRunDocument>(SummaryProjection)
            .Sort(sort)
            .Skip(skip);

        if (limit is int take)
            find = find.Limit(take);

        var documents = await find.ToListAsync(cancellationToken);
        return documents.Select(ToRow).ToList();
    }

    private static SortDefinition<AutomationRunDocument> Sort(string sortBy, bool descending)
    {
        var sort = Builders<AutomationRunDocument>.Sort;
        return sortBy switch
        {
            "runName" => descending ? sort.Descending(row => row.RunName) : sort.Ascending(row => row.RunName),
            "patientCount" => descending ? sort.Descending(row => row.PatientCount) : sort.Ascending(row => row.PatientCount),
            "seed" => descending ? sort.Descending(row => row.Seed) : sort.Ascending(row => row.Seed),
            "status" => descending ? sort.Descending(row => row.Status) : sort.Ascending(row => row.Status),
            "finishedAt" => descending ? sort.Descending(row => row.FinishedAt) : sort.Ascending(row => row.FinishedAt),
            _ => descending ? sort.Descending(row => row.CreatedAt) : sort.Ascending(row => row.CreatedAt)
        };
    }

    private static AutomationRunRow ToRow(AutomationRunDocument document) =>
        AutomationRules.ToRow(
            document.RunId,
            document.RunName,
            document.Scenario,
            document.Status,
            document.PatientCount,
            document.Seed,
            document.IsMetricsRun,
            document.CreatedAt,
            document.StartedAt,
            document.FinishedAt,
            document.Error,
            document.Duration,
            document.FacilityId,
            document.AutomationCreatedFacility,
            document.ReportId,
            document.GeneratedTemplateCacheVersionNumber,
            document.RetentionNotice);

    [BsonIgnoreExtraElements]
    private sealed class AutomationRunDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid RunId { get; set; }

        public string FacilityId { get; set; } = string.Empty;

        public bool AutomationCreatedFacility { get; set; }

        public string ReportId { get; set; } = string.Empty;

        public string RunName { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int PatientCount { get; set; }

        public int Seed { get; set; }

        public bool IsMetricsRun { get; set; }

        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset CreatedAt { get; set; }

        public bool IsActive { get; set; }

        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset? StartedAt { get; set; }

        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset? FinishedAt { get; set; }

        public string? Error { get; set; }

        public string? RetentionNotice { get; set; }

        public string? Duration { get; set; }

        public int? GeneratedTemplateCacheVersionNumber { get; set; }
    }

    [BsonIgnoreExtraElements]
    private sealed class OwnershipRunDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid RunId { get; set; }

        public string FacilityId { get; set; } = string.Empty;

        public bool AutomationCreatedFacility { get; set; }

        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset CreatedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    private sealed class OwnershipTombstoneDocument
    {
        [BsonId]
        public string FacilityId { get; set; } = string.Empty;

        public string RunId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset CreatedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    private sealed class ScenarioChoiceDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsSystemScenario { get; set; }

        public ReportMethod ReportMethod { get; set; }

        public List<ProfiledMeasureType> SelectedMeasures { get; set; } = [];

        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
