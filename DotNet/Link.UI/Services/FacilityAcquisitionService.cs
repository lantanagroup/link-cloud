using System.Text.Json;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Loads and saves the facility-hub data acquisition panels through LinkSDK.
/// </summary>
public sealed class FacilityAcquisitionService
{
    private readonly IDataAcquisitionServiceClient _client;
    private readonly ILogger _logger;

    public FacilityAcquisitionService(IDataAcquisitionServiceClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task LoadAsync(
        FacilityHubViewModel page,
        string? planType,
        int? reportingOrgId,
        CancellationToken cancellationToken)
    {
        var facilityId = page.FacilityId!;
        var selectedType = FacilityAcquisitionRules.NormalizePlanType(planType);
        var fhirTask = _client.GetFhirQueryConfigurationAsync(facilityId, cancellationToken);
        var listTask = _client.GetFhirListConfigurationAsync(facilityId, includePatients: false, cancellationToken);
        var orgTask = _client.GetOrganizationLocationConfigurationsAsync(facilityId, cancellationToken);
        var sftpTask = _client.GetOrganizationSftpConfigurationAsync(facilityId, cancellationToken);
        var credentialTask = _client.GetSftpCredentialStatusAsync(facilityId, cancellationToken);
        var planTasks = FacilityAcquisitionRules.QueryPlanTypes.ToDictionary(
            type => type,
            type => _client.GetQueryPlanAsync(facilityId, type, cancellationToken));

        page.FhirQuery = await ReadFhirQueryAsync(facilityId, fhirTask);
        page.FhirList = await ReadFhirListAsync(facilityId, listTask);
        page.ReportingOrg = await ReadReportingOrgAsync(facilityId, reportingOrgId, orgTask);
        page.Sftp = await ReadSftpAsync(facilityId, sftpTask, credentialTask);
        (page.QueryPlan, page.ExistingQueryPlanTypes, page.QueryPlanSummaries) =
            await ReadQueryPlansAsync(facilityId, selectedType, planTasks);
    }

    public async Task<string?> SaveFhirQueryAsync(FacilityHubViewModel page, FhirQueryPanel input, CancellationToken cancellationToken)
    {
        page.FhirQuery.Apply(input);
        page.FhirQuery.CustomHeaders = FacilityAcquisitionRules.WithBlankHeader(page.FhirQuery.CustomHeaders);
        if (!FacilityAcquisitionRules.TryBuildFhirQuery(input, page.FacilityId!, page.TimeZone, out var body, out var error))
            return error;

        var response = input.Exists
            ? await _client.UpdateFhirQueryConfigurationAsync(body!, cancellationToken)
            : await _client.CreateFhirQueryConfigurationAsync(body!, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("FHIR query save", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> DeleteFhirQueryAsync(FacilityHubViewModel page, CancellationToken cancellationToken)
    {
        var response = await _client.DeleteFhirQueryConfigurationAsync(page.FacilityId!, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("FHIR query delete", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> SaveFhirListAsync(FacilityHubViewModel page, FhirListPanel input, CancellationToken cancellationToken)
    {
        page.FhirList.Apply(input);
        object? preserved = null;
        if (input.Exists)
        {
            var current = await _client.GetFhirListConfigurationAsync(page.FacilityId!, includePatients: false, cancellationToken);
            if (current.IsSuccessStatusCode)
                preserved = FacilityAcquisitionRules.ExtractAuthentication(current.RawBody);
        }

        if (!FacilityAcquisitionRules.TryBuildFhirList(input, page.FacilityId!, preserved, out var body, out var error))
            return error;

        var response = input.Exists
            ? await _client.UpdateFhirListConfigurationAsync(body!, cancellationToken)
            : await _client.CreateFhirListConfigurationAsync(body!, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("FHIR list save", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> DeleteFhirListAsync(FacilityHubViewModel page, CancellationToken cancellationToken)
    {
        var response = await _client.DeleteFhirListConfigurationAsync(page.FacilityId!, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("FHIR list delete", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> SaveQueryPlanAsync(FacilityHubViewModel page, QueryPlanPanel input, CancellationToken cancellationToken)
    {
        page.QueryPlan.Apply(input);
        page.QueryPlan.Type = FacilityAcquisitionRules.NormalizePlanType(input.Type);
        page.QueryPlan.InitialQueries = FacilityAcquisitionRules.WithBlankQuery(page.QueryPlan.InitialQueries);
        page.QueryPlan.SupplementalQueries = FacilityAcquisitionRules.WithBlankQuery(page.QueryPlan.SupplementalQueries);
        if (!FacilityAcquisitionRules.TryBuildQueryPlan(input, page.FacilityId!, out var body, out var error))
            return error;

        var response = input.Exists
            ? await _client.UpdateQueryPlanAsync(page.FacilityId!, body!, cancellationToken)
            : await _client.CreateQueryPlanAsync(page.FacilityId!, ToPlan(body!), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("Query plan save", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> DeleteQueryPlanAsync(FacilityHubViewModel page, string? type, CancellationToken cancellationToken)
    {
        var planType = FacilityAcquisitionRules.NormalizePlanType(type);
        var response = await _client.DeleteQueryPlanAsync(page.FacilityId!, planType, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("Query plan delete", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> SaveReportingOrgAsync(FacilityHubViewModel page, ReportingOrgPanel input, CancellationToken cancellationToken)
    {
        page.ReportingOrg.Apply(input);
        page.ReportingOrg.Matches = FacilityAcquisitionRules.WithBlankMatch(page.ReportingOrg.Matches);
        if (!FacilityAcquisitionRules.TryBuildReportingOrg(input, out var body, out var error))
            return error;

        var response = input.Exists && input.ConfigId is int configId
            ? await _client.UpdateOrganizationLocationConfigurationByIdAsync(configId, body!, cancellationToken)
            : await _client.CreateOrganizationLocationConfigurationAsync(page.FacilityId!, body!, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("Reporting organization save", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> DeleteReportingOrgAsync(FacilityHubViewModel page, int? configId, CancellationToken cancellationToken)
    {
        if (configId is not int id)
            return "No reporting organization configuration is selected.";

        var response = await _client.DeleteOrganizationLocationConfigurationByIdAsync(id, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("Reporting organization delete", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> SaveSftpAsync(FacilityHubViewModel page, SftpPanel input, CancellationToken cancellationToken)
    {
        page.Sftp.Apply(input);
        page.Sftp.Acquisitions = FacilityAcquisitionRules.WithBlankAcquisition(page.Sftp.Acquisitions);
        if (!FacilityAcquisitionRules.TryBuildSftp(input, page.FacilityId!, out var body, out var credentials, out var error))
            return error;

        var response = input.Exists
            ? await _client.UpdateSftpConfigurationAsync(page.FacilityId!, input.ConfigurationId ?? string.Empty, body!, cancellationToken)
            : await _client.CreateSftpConfigurationAsync(page.FacilityId!, body!, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("SFTP save", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        if (!input.Exists)
        {
            page.Sftp.Exists = true;
            try
            {
                var createdId = FacilityAcquisitionRules.ParseSftp(response.RawBody).ConfigurationId;
                if (!string.IsNullOrWhiteSpace(createdId))
                    page.Sftp.ConfigurationId = createdId;
            }
            catch (JsonException)
            {
                // The configuration was stored. A missing id in the response still leaves the panel editable.
            }
        }

        if (credentials is { } pair)
        {
            var saved = await _client.UpdateSftpCredentialsAsync(
                page.FacilityId!,
                new { Username = pair.Username, Password = pair.Password },
                cancellationToken);
            if (!saved.IsSuccessStatusCode)
            {
                LogFailure("SFTP credentials save", page.FacilityId, saved);
                return "SFTP configuration was saved, but the credentials were not. "
                    + FacilityFormRules.ServiceMessage("Data acquisition", saved.StatusCode, saved.RawBody);
            }
        }

        return null;
    }

    public async Task<string?> DeleteSftpAsync(FacilityHubViewModel page, string? configurationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configurationId))
            return "No SFTP configuration is selected.";

        var response = await _client.DeleteSftpConfigurationAsync(page.FacilityId!, configurationId, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("SFTP delete", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> DeleteSftpCredentialsAsync(FacilityHubViewModel page, CancellationToken cancellationToken)
    {
        var response = await _client.DeleteSftpCredentialsAsync(page.FacilityId!, cancellationToken);
        if (!response.IsSuccessStatusCode && !IsMissing(response.StatusCode))
        {
            LogFailure("SFTP credentials delete", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> TestSavedSftpAsync(FacilityHubViewModel page, CancellationToken cancellationToken)
    {
        var response = await _client.TestSavedSftpConnectionAsync(page.FacilityId!, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogFailure("SFTP saved connection test", page.FacilityId, response);
            return FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> TestSftpAsync(FacilityHubViewModel page, SftpPanel input, CancellationToken cancellationToken)
    {
        var exists = page.Sftp.Exists;
        var configurationId = page.Sftp.ConfigurationId;
        var acquisitions = page.Sftp.Acquisitions;
        var removeAfter = page.Sftp.RemoveAfterProcessing;
        var benchmark = page.Sftp.EnableBenchmarking;
        var credentialsSaved = page.Sftp.CredentialsSaved;
        page.Sftp.Apply(input);
        page.Sftp.Exists = exists;
        page.Sftp.ConfigurationId = configurationId;
        page.Sftp.Acquisitions = acquisitions;
        page.Sftp.RemoveAfterProcessing = removeAfter;
        page.Sftp.EnableBenchmarking = benchmark;
        page.Sftp.CredentialsSaved = credentialsSaved;
        var username = input.Username?.Trim() ?? string.Empty;
        var password = input.Password ?? string.Empty;
        if (username.Length == 0 || password.Length == 0)
            return "Enter the username and password to test these details, or use Test saved connection.";

        if (!FacilityAcquisitionRules.TryBuildSftp(input, page.FacilityId!, out var body, out _, out var error))
            return error;

        var response = await _client.TestSftpConnectionAsync(new SftpTestConnectionRequestApiModel
        {
            HostName = body!["Host"]?.ToString() ?? string.Empty,
            HostUrlPort = body["Port"] is int port ? port : 22,
            Username = username,
            Password = password,
            ReportDirectory = body["RemoteDirectory"]?.ToString() ?? "/"
        }, includeFileContent: false, cancellationToken);

        if (!response.IsSuccessStatusCode || response.Body is not { Success: true })
        {
            LogFailure("SFTP connection test", page.FacilityId, response);
            var detail = response.Body?.Message;
            return string.IsNullOrWhiteSpace(detail)
                ? FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody)
                : detail;
        }

        return null;
    }

    private async Task<FhirQueryPanel> ReadFhirQueryAsync(string facilityId, Task<LinkApiResponse> task)
    {
        try
        {
            var response = await task;
            if (IsMissing(response.StatusCode))
                return FacilityAcquisitionRules.EmptyFhirQuery();
            if (!response.IsSuccessStatusCode)
            {
                LogFailure("FHIR query get", facilityId, response);
                return Failed(FacilityAcquisitionRules.EmptyFhirQuery(), true);
            }

            return FacilityAcquisitionRules.ParseFhirQuery(response.RawBody);
        }
        catch (OperationCanceledException) when (task.IsCanceled)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR query get threw for facility {FacilityId}", facilityId.Sanitize());
            var panel = FacilityAcquisitionRules.EmptyFhirQuery();
            panel.ReadFailed = true;
            return panel;
        }
    }

    private async Task<FhirListPanel> ReadFhirListAsync(string facilityId, Task<LinkApiResponse> task)
    {
        try
        {
            var response = await task;
            if (IsMissing(response.StatusCode))
                return FacilityAcquisitionRules.EmptyFhirList();
            if (!response.IsSuccessStatusCode)
            {
                LogFailure("FHIR list get", facilityId, response);
                var panel = FacilityAcquisitionRules.EmptyFhirList();
                panel.ReadFailed = true;
                return panel;
            }

            return FacilityAcquisitionRules.ParseFhirList(response.RawBody);
        }
        catch (OperationCanceledException) when (task.IsCanceled)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR list get threw for facility {FacilityId}", facilityId.Sanitize());
            var panel = FacilityAcquisitionRules.EmptyFhirList();
            panel.ReadFailed = true;
            return panel;
        }
    }

    private async Task<(QueryPlanPanel Panel, IReadOnlyList<string> Existing, IReadOnlyList<QueryPlanSummary> Summaries)> ReadQueryPlansAsync(
        string facilityId,
        string selectedType,
        Dictionary<string, Task<LinkApiResponse>> tasks)
    {
        var existing = new List<string>();
        var summaries = new List<QueryPlanSummary>();
        QueryPlanPanel? selected = null;
        foreach (var type in FacilityAcquisitionRules.QueryPlanTypes)
        {
            var task = tasks[type];
            try
            {
                var response = await task;
                if (IsMissing(response.StatusCode))
                {
                    if (type == selectedType)
                        selected = FacilityAcquisitionRules.EmptyQueryPlan(type);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    LogFailure("Query plan get", facilityId, response);
                    if (type == selectedType)
                    {
                        selected = FacilityAcquisitionRules.EmptyQueryPlan(type);
                        selected.ReadFailed = true;
                    }
                    continue;
                }

                var parsed = FacilityAcquisitionRules.ParseQueryPlan(response.RawBody, type);
                existing.Add(type);
                summaries.Add(new QueryPlanSummary
                {
                    Type = parsed.Type ?? type,
                    PlanName = parsed.PlanName,
                    LookBack = parsed.LookBack
                });
                if (type == selectedType)
                    selected = parsed;
            }
            catch (OperationCanceledException) when (task.IsCanceled)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Query plan get threw for facility {FacilityId}", facilityId.Sanitize());
                if (type == selectedType)
                {
                    selected = FacilityAcquisitionRules.EmptyQueryPlan(type);
                    selected.ReadFailed = true;
                }
            }
        }

        return (selected ?? FacilityAcquisitionRules.EmptyQueryPlan(selectedType), existing, summaries);
    }

    private async Task<ReportingOrgPanel> ReadReportingOrgAsync(
        string facilityId,
        int? reportingOrgId,
        Task<LinkApiResponse<List<OrganizationLocationConfigurationApiModel>>> task)
    {
        try
        {
            var response = await task;
            if (IsMissing(response.StatusCode))
                return FacilityAcquisitionRules.EmptyReportingOrg();
            if (!response.IsSuccessStatusCode)
            {
                LogFailure("Reporting organization get", facilityId, response);
                var failed = FacilityAcquisitionRules.EmptyReportingOrg();
                failed.ReadFailed = true;
                return failed;
            }

            return FacilityAcquisitionRules.ParseReportingOrg(response.Body, reportingOrgId);
        }
        catch (OperationCanceledException) when (task.IsCanceled)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reporting organization get threw for facility {FacilityId}", facilityId.Sanitize());
            var panel = FacilityAcquisitionRules.EmptyReportingOrg();
            panel.ReadFailed = true;
            return panel;
        }
    }

    private async Task<SftpPanel> ReadSftpAsync(
        string facilityId,
        Task<LinkApiResponse> configTask,
        Task<LinkApiResponse> credentialTask)
    {
        SftpPanel panel;
        try
        {
            var response = await configTask;
            if (IsMissing(response.StatusCode))
                panel = FacilityAcquisitionRules.EmptySftp();
            else if (!response.IsSuccessStatusCode)
            {
                LogFailure("SFTP get", facilityId, response);
                panel = FacilityAcquisitionRules.EmptySftp();
                panel.ReadFailed = true;
            }
            else
                panel = FacilityAcquisitionRules.ParseSftp(response.RawBody);
        }
        catch (OperationCanceledException) when (configTask.IsCanceled)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP get threw for facility {FacilityId}", facilityId.Sanitize());
            panel = FacilityAcquisitionRules.EmptySftp();
            panel.ReadFailed = true;
        }

        try
        {
            var status = await credentialTask;
            if (status.IsSuccessStatusCode)
                panel.CredentialsSaved = FacilityAcquisitionRules.ParseCredentialStatus(status.RawBody);
            else if (!IsMissing(status.StatusCode))
                LogFailure("SFTP credential status", facilityId, status);
        }
        catch (OperationCanceledException) when (credentialTask.IsCanceled)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP credential status threw for facility {FacilityId}", facilityId.Sanitize());
        }

        return panel;
    }

    private static FhirQueryPanel Failed(FhirQueryPanel panel, bool failed)
    {
        panel.ReadFailed = failed;
        return panel;
    }

    private static CreateQueryPlanRequestApiModel ToPlan(Dictionary<string, object?> body) => new()
    {
        PlanName = body["PlanName"]?.ToString(),
        FacilityId = body["FacilityId"]?.ToString() ?? string.Empty,
        EHRDescription = body["EHRDescription"]?.ToString() ?? string.Empty,
        LookBack = body["LookBack"]?.ToString() ?? string.Empty,
        Type = body["Type"]?.ToString() ?? string.Empty,
        InitialQueries = CastQueries(body["InitialQueries"]),
        SupplementalQueries = CastQueries(body["SupplementalQueries"])
    };

    private static Dictionary<string, object> CastQueries(object? value)
    {
        if (value is not Dictionary<string, object?> source)
            return new Dictionary<string, object>();

        return source.ToDictionary(pair => pair.Key, pair => pair.Value ?? new object());
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
}
