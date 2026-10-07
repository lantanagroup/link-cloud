using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

public sealed class FacilityHubService
{
    private readonly IFacilityServiceClient _facilities;
    private readonly ICensusServiceClient? _census;
    private readonly IQueryDispatchServiceClient? _queryDispatch;
    private readonly LinkUiFeatureOptions _options;
    private readonly ILogger<FacilityHubService> _logger;

    public FacilityHubService(
        IFacilityServiceClient facilities,
        ICensusServiceClient? census,
        IQueryDispatchServiceClient? queryDispatch,
        IOptions<LinkUiFeatureOptions> options,
        ILogger<FacilityHubService> logger)
    {
        _facilities = facilities;
        _census = census;
        _queryDispatch = queryDispatch;
        _options = options.Value;
        _logger = logger;
    }

    public static FacilityHubService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        ICensusServiceClient? census = null;
        if (!string.IsNullOrWhiteSpace(registry.CensusServiceUrl))
            census = services.GetRequiredService<ICensusServiceClient>();

        IQueryDispatchServiceClient? queryDispatch = null;
        if (!string.IsNullOrWhiteSpace(registry.QueryDispatchServiceUrl))
            queryDispatch = services.GetRequiredService<IQueryDispatchServiceClient>();

        return new FacilityHubService(
            services.GetRequiredService<IFacilityServiceClient>(),
            census,
            queryDispatch,
            services.GetRequiredService<IOptions<LinkUiFeatureOptions>>(),
            services.GetRequiredService<ILogger<FacilityHubService>>());
    }

    public async Task<FacilityHubViewModel> LoadCreateAsync(CancellationToken cancellationToken)
    {
        var vendors = await LoadVendorsAsync(cancellationToken);
        return CreateShell(vendors);
    }

    public async Task<FacilityHubViewModel> LoadEditAsync(string? facilityId, CancellationToken cancellationToken)
    {
        var id = facilityId?.Trim() ?? string.Empty;
        if (!FacilityFormRules.IsValidFacilityId(id, _options.NumericOnlyFacilityId))
        {
            return new FacilityHubViewModel
            {
                LoadError = FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId),
                DmrpEnabled = _options.DmrpEnabled
            };
        }

        var facilityTask = _facilities.GetAsync(id, cancellationToken);
        var vendorTask = LoadVendorsAsync(cancellationToken);
        await Task.WhenAll(facilityTask, vendorTask);

        var vendors = await vendorTask;
        var facility = await facilityTask;
        if (IsMissing(facility.StatusCode))
        {
            return new FacilityHubViewModel
            {
                NotFound = true,
                FacilityId = id,
                DmrpEnabled = _options.DmrpEnabled
            };
        }

        if (!facility.IsSuccessStatusCode || facility.Body is null)
        {
            LogFailure("Facility get", id, facility);
            return new FacilityHubViewModel
            {
                FacilityId = id,
                LoadError = FacilityFormRules.ServiceMessage("Tenant", facility.StatusCode, facility.RawBody),
                DmrpEnabled = _options.DmrpEnabled
            };
        }

        var page = FromFacility(facility.Body, vendors);
        await LoadPanelsAsync(page, id, cancellationToken);
        return page;
    }

    public async Task<FacilityWriteResult> CreateAsync(FacilityEditInput input, CancellationToken cancellationToken)
    {
        var vendors = await LoadVendorsAsync(cancellationToken);
        var page = CreateShell(vendors);
        ApplyInput(page, input);

        if (!TryBuild(input, vendors, currentVendorVersionId: null, out var model, out var error))
        {
            page.FormError = error;
            return FacilityWriteResult.Stay(page);
        }

        var response = await _facilities.CreateAsync(model!, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogFailure("Facility create", model!.FacilityId, response);
            page.FormError = FacilityFormRules.ServiceMessage("Tenant", response.StatusCode, response.RawBody);
            return FacilityWriteResult.Stay(page);
        }

        return FacilityWriteResult.ToFacility(model!.FacilityId!, "Facility created.");
    }

    public async Task<FacilityWriteResult> UpdateAsync(
        string? facilityId,
        FacilityEditInput input,
        CancellationToken cancellationToken)
    {
        var id = facilityId?.Trim() ?? string.Empty;
        var page = await LoadEditAsync(id, cancellationToken);
        if (page.NotFound || page.LoadError is not null)
            return FacilityWriteResult.Stay(page);

        var currentVendor = ParseGuid(page.VendorVersionId);
        ApplyInput(page, input);
        if (!string.Equals(input.FacilityId?.Trim(), id, StringComparison.Ordinal))
        {
            page.FormError = "Facility ID does not match this page.";
            return FacilityWriteResult.Stay(page);
        }

        if (!TryBuild(input, page, currentVendor, out var model, out var error))
        {
            page.FormError = error;
            return FacilityWriteResult.Stay(page);
        }

        var response = await _facilities.UpdateAsync(id, model!, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogFailure("Facility update", id, response);
            page.FormError = FacilityFormRules.ServiceMessage("Tenant", response.StatusCode, response.RawBody);
            return FacilityWriteResult.Stay(page);
        }

        return FacilityWriteResult.ToFacility(id, "Facility saved.");
    }

    public async Task<FacilityWriteResult> SaveCensusAsync(
        string? facilityId,
        bool enabled,
        string? scheduledTrigger,
        bool censusExists,
        CancellationToken cancellationToken)
    {
        var page = await RequireFacilityAsync(facilityId, cancellationToken);
        if (page.LoadError is not null || page.NotFound)
            return FacilityWriteResult.Stay(page);

        page.CensusEnabled = enabled;
        page.CensusTrigger = scheduledTrigger?.Trim();
        page.CensusExists = censusExists;

        if (_census is null)
        {
            page.CensusError = "Census service URL is not configured.";
            return FacilityWriteResult.Stay(page);
        }

        if (string.IsNullOrWhiteSpace(page.CensusTrigger))
        {
            page.CensusError = "Scheduled trigger is required. Use a Quartz cron expression such as 0 0 6 * * ?";
            return FacilityWriteResult.Stay(page);
        }

        var request = new CensusConfigApiModel
        {
            FacilityId = page.FacilityId!,
            Enabled = enabled,
            ScheduledTrigger = page.CensusTrigger
        };

        var response = censusExists
            ? await _census.UpdateCensusConfigAsync(page.FacilityId!, request, cancellationToken)
            : await _census.CreateCensusConfigAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("Census save", page.FacilityId, response);
            page.CensusError = FacilityFormRules.ServiceMessage("Census", response.StatusCode, response.RawBody);
            return FacilityWriteResult.Stay(page);
        }

        return FacilityWriteResult.ToFacility(page.FacilityId!, censusExists
            ? "Census configuration saved."
            : "Census configuration created.");
    }

    public async Task<FacilityWriteResult> DeleteCensusAsync(string? facilityId, CancellationToken cancellationToken)
    {
        var page = await RequireFacilityAsync(facilityId, cancellationToken);
        if (page.LoadError is not null || page.NotFound)
            return FacilityWriteResult.Stay(page);

        if (_census is null)
        {
            page.CensusError = "Census service URL is not configured.";
            return FacilityWriteResult.Stay(page);
        }

        var response = await _census.DeleteCensusConfigAsync(page.FacilityId!, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("Census delete", page.FacilityId, response);
            page.CensusError = FacilityFormRules.ServiceMessage("Census", response.StatusCode, response.RawBody);
            return FacilityWriteResult.Stay(page);
        }

        return FacilityWriteResult.ToFacility(page.FacilityId!, "Census configuration deleted.");
    }

    public async Task<FacilityWriteResult> SaveQueryDispatchAsync(
        string? facilityId,
        List<DispatchScheduleInput>? schedules,
        bool queryDispatchExists,
        CancellationToken cancellationToken)
    {
        var page = await RequireFacilityAsync(facilityId, cancellationToken);
        if (page.LoadError is not null || page.NotFound)
            return FacilityWriteResult.Stay(page);

        page.QueryDispatchExists = queryDispatchExists;
        page.Schedules = FacilityFormRules.WithBlankRow(schedules);

        if (_queryDispatch is null)
        {
            page.QueryDispatchError = "Query dispatch service URL is not configured.";
            return FacilityWriteResult.Stay(page);
        }

        if (!FacilityFormRules.TryNormalizeSchedules(schedules, out var normalized, out var error))
        {
            page.QueryDispatchError = error;
            return FacilityWriteResult.Stay(page);
        }

        var request = new QueryDispatchConfigurationApiModel
        {
            FacilityId = page.FacilityId!,
            DispatchSchedules = normalized
        };

        var response = queryDispatchExists
            ? await _queryDispatch.UpsertQueryDispatchConfigurationAsync(page.FacilityId!, request, cancellationToken)
            : await _queryDispatch.CreateQueryDispatchConfigurationAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("Query dispatch save", page.FacilityId, response);
            page.QueryDispatchError = FacilityFormRules.ServiceMessage("Query dispatch", response.StatusCode, response.RawBody);
            return FacilityWriteResult.Stay(page);
        }

        return FacilityWriteResult.ToFacility(page.FacilityId!, queryDispatchExists
            ? "Query dispatch configuration saved."
            : "Query dispatch configuration created.");
    }

    public async Task<FacilityWriteResult> DeleteQueryDispatchAsync(string? facilityId, CancellationToken cancellationToken)
    {
        var page = await RequireFacilityAsync(facilityId, cancellationToken);
        if (page.LoadError is not null || page.NotFound)
            return FacilityWriteResult.Stay(page);

        if (_queryDispatch is null)
        {
            page.QueryDispatchError = "Query dispatch service URL is not configured.";
            return FacilityWriteResult.Stay(page);
        }

        var response = await _queryDispatch.DeleteQueryDispatchConfigurationAsync(page.FacilityId!, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("Query dispatch delete", page.FacilityId, response);
            page.QueryDispatchError = FacilityFormRules.ServiceMessage("Query dispatch", response.StatusCode, response.RawBody);
            return FacilityWriteResult.Stay(page);
        }

        return FacilityWriteResult.ToFacility(page.FacilityId!, "Query dispatch configuration deleted.");
    }

    public async Task<FacilityWriteResult> SoftDeleteAsync(string? facilityId, CancellationToken cancellationToken)
    {
        var id = facilityId?.Trim() ?? string.Empty;
        if (!FacilityFormRules.IsValidFacilityId(id, _options.NumericOnlyFacilityId))
        {
            return FacilityWriteResult.Stay(new FacilityHubViewModel
            {
                LoadError = FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId)
            });
        }

        var response = await _facilities.SoftDeleteAsync(id, cancellationToken);
        // Tenant answers a successful delete with 204. That status means "missing" on GET, not here.
        if (response.StatusCode == StatusCodes.Status404NotFound)
        {
            return FacilityWriteResult.Stay(new FacilityHubViewModel
            {
                NotFound = true,
                FacilityId = id
            });
        }

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("Facility remove", id, response);
            var page = await LoadEditAsync(id, cancellationToken);
            page.FormError = FacilityFormRules.ServiceMessage("Tenant", response.StatusCode, response.RawBody);
            return FacilityWriteResult.Stay(page);
        }

        return FacilityWriteResult.ToList("Facility removed.");
    }

    private async Task<FacilityHubViewModel> RequireFacilityAsync(string? facilityId, CancellationToken cancellationToken)
    {
        var page = await LoadEditAsync(facilityId, cancellationToken);
        return page;
    }

    private FacilityHubViewModel CreateShell(VendorLoad vendors) => new()
    {
        IsCreate = true,
        DmrpEnabled = _options.DmrpEnabled,
        VendorListLoaded = vendors.Loaded,
        VendorWarning = vendors.Warning,
        Vendors = vendors.Options,
        TimeZones = FacilityFormRules.TimeZones(null),
        CensusEnabled = true,
        Schedules = FacilityFormRules.WithBlankRow(null)
    };

    private FacilityHubViewModel FromFacility(FacilityModel facility, VendorLoad vendors)
    {
        var reports = facility.ScheduledReports ?? FacilityFormRules.EmptySchedule();
        var vendorId = facility.VendorVersionId;
        var options = vendors.Options.ToList();
        if (vendorId is Guid current && options.All(option => option.Id != current))
        {
            options.Insert(0, new VendorOption { Id = current, Label = current.ToString() });
        }

        return new FacilityHubViewModel
        {
            FacilityId = facility.FacilityId,
            FacilityName = facility.FacilityName,
            TimeZone = facility.TimeZone,
            VendorVersionId = vendorId?.ToString(),
            VendorListLoaded = vendors.Loaded,
            VendorWarning = vendors.Warning,
            Vendors = options,
            TimeZones = FacilityFormRules.TimeZones(facility.TimeZone),
            DmrpEnabled = _options.DmrpEnabled,
            DailyReports = FacilityFormRules.JoinReports(reports.Daily),
            WeeklyReports = FacilityFormRules.JoinReports(reports.Weekly),
            MonthlyReports = FacilityFormRules.JoinReports(reports.Monthly),
            CensusConfigured = _census is not null,
            QueryDispatchConfigured = _queryDispatch is not null,
            CensusEnabled = true,
            Schedules = FacilityFormRules.WithBlankRow(null)
        };
    }

    private async Task LoadPanelsAsync(FacilityHubViewModel page, string facilityId, CancellationToken cancellationToken)
    {
        Task<LinkApiResponse<CensusConfigApiModel>>? censusTask = _census?.GetCensusConfigAsync(facilityId, cancellationToken);
        Task<LinkApiResponse<QueryDispatchConfigurationApiModel>>? dispatchTask =
            _queryDispatch?.GetConfigurationAsync(facilityId, cancellationToken);

        if (censusTask is not null)
            await ApplyCensusAsync(page, facilityId, censusTask);

        if (dispatchTask is not null)
            await ApplyQueryDispatchAsync(page, facilityId, dispatchTask);
    }

    private async Task ApplyCensusAsync(
        FacilityHubViewModel page,
        string facilityId,
        Task<LinkApiResponse<CensusConfigApiModel>> censusTask)
    {
        try
        {
            var census = await censusTask;
            if (IsMissing(census.StatusCode))
            {
                page.CensusExists = false;
                return;
            }

            if (!census.IsSuccessStatusCode || census.Body is null)
            {
                LogFailure("Census get", facilityId, census);
                page.CensusError = FacilityFormRules.ServiceMessage("Census", census.StatusCode, census.RawBody);
                return;
            }

            page.CensusExists = true;
            page.CensusEnabled = census.Body.Enabled ?? true;
            page.CensusTrigger = census.Body.ScheduledTrigger;
        }
        catch (OperationCanceledException) when (censusTask.IsCanceled)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Census get threw for facility {FacilityId}", facilityId.Sanitize());
            page.CensusError = "Census service call failed.";
        }
    }

    private async Task ApplyQueryDispatchAsync(
        FacilityHubViewModel page,
        string facilityId,
        Task<LinkApiResponse<QueryDispatchConfigurationApiModel>> dispatchTask)
    {
        try
        {
            var dispatch = await dispatchTask;
            if (IsMissing(dispatch.StatusCode))
            {
                page.QueryDispatchExists = false;
                page.Schedules = FacilityFormRules.WithBlankRow(null);
                return;
            }

            if (!dispatch.IsSuccessStatusCode || dispatch.Body is null)
            {
                LogFailure("Query dispatch get", facilityId, dispatch);
                page.QueryDispatchError = FacilityFormRules.ServiceMessage("Query dispatch", dispatch.StatusCode, dispatch.RawBody);
                return;
            }

            page.QueryDispatchExists = true;
            var rows = dispatch.Body.DispatchSchedules.Select(schedule => new DispatchScheduleInput
            {
                Event = schedule.Event,
                Duration = schedule.Duration
            });
            page.Schedules = FacilityFormRules.WithBlankRow(rows);
        }
        catch (OperationCanceledException) when (dispatchTask.IsCanceled)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query dispatch get threw for facility {FacilityId}", facilityId.Sanitize());
            page.QueryDispatchError = "Query dispatch service call failed.";
        }
    }

    private async Task<VendorLoad> LoadVendorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _facilities.GetVendorVersionsAsync(cancellationToken: cancellationToken);
            if (response.StatusCode == StatusCodes.Status204NoContent || (response.IsSuccessStatusCode && response.Body is null))
            {
                return new VendorLoad(true, Array.Empty<VendorOption>(), null);
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                LogFailure("Vendor versions", null, response);
                return new VendorLoad(
                    false,
                    Array.Empty<VendorOption>(),
                    FacilityFormRules.ServiceMessage("Tenant", response.StatusCode, response.RawBody));
            }

            var options = response.Body
                .Where(version => version.Id is Guid id && id != Guid.Empty)
                .OrderBy(version => version.VendorName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
                .Select(version => new VendorOption
                {
                    Id = version.Id!.Value,
                    Label = string.IsNullOrWhiteSpace(version.VendorName)
                        ? version.Version ?? version.Id.ToString()!
                        : $"{version.VendorName} - {version.Version}"
                })
                .ToList();

            return new VendorLoad(true, options, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vendor version list threw");
            return new VendorLoad(false, Array.Empty<VendorOption>(), "Tenant service call failed while loading vendors.");
        }
    }

    private bool TryBuild(
        FacilityEditInput input,
        FacilityHubViewModel page,
        Guid? currentVendorVersionId,
        out FacilityModel? model,
        out string? error) =>
        TryBuild(input, new VendorLoad(page.VendorListLoaded, page.Vendors, page.VendorWarning), currentVendorVersionId, out model, out error);

    private bool TryBuild(
        FacilityEditInput input,
        VendorLoad vendors,
        Guid? currentVendorVersionId,
        out FacilityModel? model,
        out string? error)
    {
        var allowed = vendors.Options.Select(option => option.Id).ToHashSet();
        if (currentVendorVersionId is Guid current)
            allowed.Add(current);

        return FacilityFormRules.TryBuildFacility(
            input,
            _options.DmrpEnabled,
            _options.NumericOnlyFacilityId,
            vendors.Loaded,
            currentVendorVersionId,
            allowed,
            out model,
            out error);
    }

    private static void ApplyInput(FacilityHubViewModel page, FacilityEditInput input)
    {
        page.FacilityId = input.FacilityId?.Trim();
        page.FacilityName = input.FacilityName?.Trim();
        page.TimeZone = input.TimeZone?.Trim();
        page.TimeZones = FacilityFormRules.TimeZones(page.TimeZone);
        if (page.VendorListLoaded)
            page.VendorVersionId = string.IsNullOrWhiteSpace(input.VendorVersionId) ? null : input.VendorVersionId.Trim();

        if (!page.DmrpEnabled)
        {
            page.DailyReports = input.DailyReports;
            page.WeeklyReports = input.WeeklyReports;
            page.MonthlyReports = input.MonthlyReports;
        }
    }

    private void LogFailure(string operation, string? facilityId, LinkApiResponse response)
    {
        _logger.LogWarning(
            "{Operation} failed with status {StatusCode}. FacilityId={FacilityId} TraceId={TraceId}",
            operation,
            response.StatusCode,
            facilityId?.Sanitize(),
            response.TraceId);
    }

    private static bool IsMissing(int statusCode) =>
        statusCode is StatusCodes.Status404NotFound or StatusCodes.Status204NoContent;

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed : null;

    private sealed record VendorLoad(bool Loaded, IReadOnlyList<VendorOption> Options, string? Warning);
}
