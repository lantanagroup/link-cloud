using System.Globalization;
using System.Text.Json;
using System.Xml;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Form rules for the facility-hub data acquisition panels. Payloads match the Data Acquisition
/// controllers: FHIR query, FHIR list (exactly six patient lists), query plan, reporting
/// organization, and SFTP. SFTP passwords stay off the configuration body.
/// </summary>
public static class FacilityAcquisitionRules
{
    public const int MaxConcurrentLower = 1;
    public const int MaxConcurrentUpper = 16;
    public const int MaxRetriesLower = 0;
    public const int MaxRetriesUpper = 10;
    public const int MaxTimeoutMinutes = 30;
    public const int MaxHostLength = 256;
    public const int MaxPathLength = 4096;

    public static readonly string[] QueryPlanTypes = ["Discharge", "Daily", "Weekly", "Monthly"];
    public static readonly string[] FhirAuthTypes = ["Basic", "Epic", "OAuth", "CustomHeaders"];
    public static readonly string[] QueryConfigTypes = ["Parameter", "Reference"];
    public static readonly string[] OperationTypes = ["Read", "Search", "SearchPost"];
    public static readonly string[] ParameterTypes = ["Variable", "Literal", "ResourceIds"];
    public static readonly string[] Variables = ["PatientId", "LookbackStart", "PeriodStart", "PeriodEnd"];
    public static readonly string[] SetupMethods = ["manual", "identifier", "managingOrg", "locationType"];
    public static readonly string[] AcquisitionTypes = ["Census", "Resources"];
    public static readonly string[] AcquisitionSubTypes = ["None", "CernerCCLExtract"];

    public static readonly (string Status, string TimeFrame, string Label)[] PatientListSlots =
    [
        ("Admit", "LessThan24Hours", "Admit, under 24 hours"),
        ("Admit", "Between24To48Hours", "Admit, 24 to 48 hours"),
        ("Admit", "MoreThan48Hours", "Admit, over 48 hours"),
        ("Discharge", "LessThan24Hours", "Discharge, under 24 hours"),
        ("Discharge", "Between24To48Hours", "Discharge, 24 to 48 hours"),
        ("Discharge", "MoreThan48Hours", "Discharge, over 48 hours")
    ];

    public static FhirQueryPanel EmptyFhirQuery() => new()
    {
        MaxConcurrentRequests = 1,
        MaxRetries = 0,
        AuthType = "Epic",
        CustomHeaders = WithBlankHeader(null)
    };

    public static FhirListPanel EmptyFhirList() => new()
    {
        Lists = PatientListSlots.Select(slot => new PatientListInput
        {
            Status = slot.Status,
            TimeFrame = slot.TimeFrame
        }).ToList()
    };

    public static QueryPlanPanel EmptyQueryPlan(string? type) => new()
    {
        Type = NormalizePlanType(type),
        InitialQueries = WithBlankQuery(null),
        SupplementalQueries = WithBlankQuery(null)
    };

    public static ReportingOrgPanel EmptyReportingOrg() => new()
    {
        IsActive = true,
        SetupMethod = "manual",
        Matches = WithBlankMatch(null)
    };

    public static SftpPanel EmptySftp() => new()
    {
        Port = 22,
        RemoteDirectory = "/",
        Timeout = "00:01:00",
        Acquisitions = WithBlankAcquisition(null)
    };

    public static string NormalizePlanType(string? type)
    {
        var match = QueryPlanTypes.FirstOrDefault(item => string.Equals(item, type?.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? QueryPlanTypes[0];
    }

    public static FhirQueryPanel ParseFhirQuery(string? json)
    {
        var panel = EmptyFhirQuery();
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = document.RootElement;
        panel.Exists = true;
        panel.Id = StringOf(root, "id");
        panel.FhirServerBaseUrl = StringOf(root, "fhirServerBaseUrl");
        panel.MaxConcurrentRequests = IntOf(root, "maxConcurrentRequests") ?? 1;
        panel.MaxRetries = IntOf(root, "maxRetries") ?? 0;
        panel.MinPull = TimeOf(root, "minAcquisitionPullTime");
        panel.MaxPull = TimeOf(root, "maxAcquisitionPullTime");
        if (TryProp(root, "authentication", out var auth) && auth.ValueKind == JsonValueKind.Object)
        {
            var authType = StringOf(auth, "authType");
            if (!string.IsNullOrWhiteSpace(authType) && !string.Equals(authType, "None", StringComparison.OrdinalIgnoreCase))
            {
                panel.AuthEnabled = true;
                panel.AuthType = authType;
                panel.AuthKey = StringOf(auth, "key");
                panel.TokenUrl = StringOf(auth, "tokenUrl");
                panel.Audience = StringOf(auth, "audience");
                panel.ClientId = StringOf(auth, "clientId");
                panel.ClientSecret = StringOf(auth, "clientSecret");
                panel.Scope = StringOf(auth, "scope");
                panel.UserName = StringOf(auth, "userName");
                panel.Password = StringOf(auth, "password");
                panel.CustomHeaders = ReadHeaders(auth);
            }
        }

        panel.CustomHeaders = WithBlankHeader(panel.CustomHeaders);
        return panel;
    }

    public static FhirListPanel ParseFhirList(string? json)
    {
        var panel = EmptyFhirList();
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = document.RootElement;
        panel.Exists = true;
        panel.FhirBaseServerUrl = StringOf(root, "fhirBaseServerUrl");
        if (!TryProp(root, "ehrPatientLists", out var lists) || lists.ValueKind != JsonValueKind.Array)
            return panel;

        foreach (var item in lists.EnumerateArray())
        {
            var status = StringOf(item, "status");
            var timeFrame = StringOf(item, "timeFrame");
            var slot = panel.Lists.FirstOrDefault(row =>
                string.Equals(row.Status, status, StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.TimeFrame, timeFrame, StringComparison.OrdinalIgnoreCase));
            if (slot is not null)
                slot.FhirId = StringOf(item, "fhirId");
        }

        return panel;
    }

    public static object? ExtractAuthentication(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        if (!TryProp(document.RootElement, "authentication", out var auth) || auth.ValueKind != JsonValueKind.Object)
            return null;

        return ToPlain(auth);
    }

    public static QueryPlanPanel ParseQueryPlan(string? json, string type)
    {
        var panel = EmptyQueryPlan(type);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = document.RootElement;
        panel.Exists = true;
        panel.Type = NormalizePlanType(StringOf(root, "type") ?? type);
        panel.PlanName = StringOf(root, "planName");
        panel.EhrDescription = StringOf(root, "ehrDescription");
        panel.LookBack = StringOf(root, "lookBack");
        panel.InitialQueries = ReadQueries(root, "initialQueries");
        panel.SupplementalQueries = ReadQueries(root, "supplementalQueries");
        return panel;
    }

    public static ReportingOrgPanel ParseReportingOrg(
        IReadOnlyList<OrganizationLocationConfigurationApiModel>? configs,
        int? selectedId)
    {
        var panel = EmptyReportingOrg();
        var rows = configs ?? Array.Empty<OrganizationLocationConfigurationApiModel>();
        panel.Choices = rows
            .OrderByDescending(row => row.IsActive)
            .ThenBy(row => row.ConfigId)
            .Select(row => new ReportingOrgChoice
            {
                Id = row.ConfigId,
                Description = row.Description,
                IsActive = row.IsActive
            })
            .ToList();

        if (panel.Choices.Count == 0)
            return panel;

        var selected = rows.FirstOrDefault(row => selectedId is int id && row.ConfigId == id)
            ?? rows.FirstOrDefault(row => row.IsActive)
            ?? rows[0];

        panel.Exists = true;
        panel.ConfigId = selected.ConfigId;
        panel.Description = selected.Description;
        panel.IsActive = selected.IsActive;
        var path = JoinPaths(selected.Conditions?.Select(condition => condition.FhirPath));
        ApplyPath(panel, path);
        return panel;
    }

    public static SftpPanel ParseSftp(string? json)
    {
        var panel = EmptySftp();
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = document.RootElement;
        panel.Exists = true;
        panel.ConfigurationId = StringOf(root, "id");
        panel.Host = StringOf(root, "host");
        panel.Port = IntOf(root, "port") ?? 22;
        panel.RemoteDirectory = StringOf(root, "remoteDirectory") ?? "/";
        panel.Timeout = TimeOf(root, "timeout") ?? "00:01:00";
        panel.RemoveAfterProcessing = BoolOf(root, "removeAfterProcessing");
        panel.EnableBenchmarking = BoolOf(root, "enableBenchmarking");
        panel.Acquisitions = ReadAcquisitions(root);
        return panel;
    }

    public static bool? ParseCredentialStatus(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        if (!TryProp(document.RootElement, "hasCredentials", out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    public static bool TryBuildFhirQuery(
        FhirQueryPanel input,
        string facilityId,
        string? timeZone,
        out Dictionary<string, object?>? body,
        out string? error)
    {
        body = null;
        var url = input.FhirServerBaseUrl?.Trim() ?? string.Empty;
        if (!IsHttpUrl(url))
        {
            error = "FHIR server base URL must be an absolute http or https address.";
            return false;
        }

        if (input.MaxConcurrentRequests is not int concurrent
            || concurrent < MaxConcurrentLower
            || concurrent > MaxConcurrentUpper)
        {
            error = $"Max concurrent requests must be from {MaxConcurrentLower} to {MaxConcurrentUpper}.";
            return false;
        }

        if (input.MaxRetries is not int retries || retries < MaxRetriesLower || retries > MaxRetriesUpper)
        {
            error = $"Max retries must be from {MaxRetriesLower} to {MaxRetriesUpper}.";
            return false;
        }

        if (!TryPullPair(input.MinPull, input.MaxPull, out var min, out var max, out error))
            return false;

        if (!TryAuthentication(input, out var authentication, out error))
            return false;

        body = new Dictionary<string, object?>
        {
            ["FacilityId"] = facilityId,
            ["FhirServerBaseUrl"] = url,
            ["MaxConcurrentRequests"] = concurrent,
            ["MaxRetries"] = retries,
            ["TimeZone"] = string.IsNullOrWhiteSpace(timeZone) ? null : timeZone.Trim()
        };
        if (!string.IsNullOrWhiteSpace(input.Id))
            body["Id"] = input.Id.Trim();
        if (min is not null)
            body["MinAcquisitionPullTime"] = min;
        if (max is not null)
            body["MaxAcquisitionPullTime"] = max;
        if (authentication is not null)
            body["Authentication"] = authentication;

        error = null;
        return true;
    }

    public static bool TryBuildFhirList(
        FhirListPanel input,
        string facilityId,
        object? preservedAuthentication,
        out Dictionary<string, object?>? body,
        out string? error)
    {
        body = null;
        var url = input.FhirBaseServerUrl?.Trim() ?? string.Empty;
        if (!IsHttpUrl(url))
        {
            error = "FHIR server base URL must be an absolute http or https address.";
            return false;
        }

        var posted = input.Lists ?? new List<PatientListInput>();
        var rows = new List<Dictionary<string, object?>>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in PatientListSlots)
        {
            var match = posted.FirstOrDefault(row =>
                string.Equals(row.Status, slot.Status, StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.TimeFrame, slot.TimeFrame, StringComparison.OrdinalIgnoreCase));
            var fhirId = match?.FhirId?.Trim() ?? string.Empty;
            if (fhirId.Length == 0)
            {
                error = $"FHIR id is required for {slot.Label}.";
                return false;
            }

            if (!seenIds.Add(fhirId))
            {
                error = $"FHIR id {fhirId} is used on more than one patient list.";
                return false;
            }

            rows.Add(new Dictionary<string, object?>
            {
                ["Status"] = slot.Status,
                ["TimeFrame"] = slot.TimeFrame,
                ["FhirId"] = fhirId
            });
        }

        body = new Dictionary<string, object?>
        {
            ["FacilityId"] = facilityId,
            ["FhirBaseServerUrl"] = url,
            ["EHRPatientLists"] = rows
        };
        if (preservedAuthentication is not null)
            body["Authentication"] = preservedAuthentication;

        error = null;
        return true;
    }

    public static bool TryBuildQueryPlan(
        QueryPlanPanel input,
        string facilityId,
        out Dictionary<string, object?>? body,
        out string? error)
    {
        body = null;
        var type = NormalizePlanType(input.Type);
        if (!QueryPlanTypes.Contains(type, StringComparer.Ordinal))
        {
            error = "Query plan type must be Discharge, Daily, Weekly, or Monthly.";
            return false;
        }

        var name = input.PlanName?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 200)
        {
            error = "Plan name is required and must be 200 characters or fewer.";
            return false;
        }

        var description = input.EhrDescription?.Trim() ?? string.Empty;
        if (description.Length is < 1 or > 500)
        {
            error = "EHR description is required and must be 500 characters or fewer.";
            return false;
        }

        var lookBack = input.LookBack?.Trim() ?? string.Empty;
        if (lookBack.Length is < 1 or > 50 || !FacilityFormRules.IsDuration(lookBack))
        {
            error = "Look back is required. Use an ISO 8601 duration such as P30D.";
            return false;
        }

        if (!TryQueries(input.InitialQueries, "Initial", requireUniqueResources: true, out var initial, out error)
            || !TryQueries(input.SupplementalQueries, "Supplemental", requireUniqueResources: false, out var supplemental, out error))
        {
            return false;
        }

        body = new Dictionary<string, object?>
        {
            ["PlanName"] = name,
            ["FacilityId"] = facilityId,
            ["EHRDescription"] = description,
            ["LookBack"] = lookBack,
            ["Type"] = type,
            ["InitialQueries"] = initial,
            ["SupplementalQueries"] = supplemental
        };
        error = null;
        return true;
    }

    public static bool TryBuildReportingOrg(
        ReportingOrgPanel input,
        out CreateOrganizationLocationConfigurationApiModel? body,
        out string? error)
    {
        body = null;
        var method = SetupMethods.Contains(input.SetupMethod ?? string.Empty, StringComparer.Ordinal)
            ? input.SetupMethod!
            : "manual";

        string path;
        if (method == "manual")
        {
            path = input.FhirPath?.Trim() ?? string.Empty;
            if (path.Length == 0)
            {
                error = "FHIRPath is required.";
                return false;
            }
        }
        else if (!TryMatches(input.Matches, method, out path, out error))
        {
            return false;
        }

        if (path.Length > 4000)
        {
            error = "FHIRPath must be 4000 characters or fewer.";
            return false;
        }

        var description = input.Description?.Trim();
        if (description is { Length: > 500 })
        {
            error = "Description must be 500 characters or fewer.";
            return false;
        }

        body = new CreateOrganizationLocationConfigurationApiModel
        {
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            IsActive = input.IsActive,
            Conditions =
            [
                new CreateOrganizationLocationConditionApiModel
                {
                    FhirPath = path,
                    Priority = 1
                }
            ]
        };
        error = null;
        return true;
    }

    public static bool TryBuildSftp(
        SftpPanel input,
        string facilityId,
        out Dictionary<string, object?>? body,
        out (string Username, string Password)? credentials,
        out string? error)
    {
        body = null;
        credentials = null;
        var host = input.Host?.Trim() ?? string.Empty;
        if (!IsHost(host))
        {
            error = "Host is required and must be a hostname or IP address.";
            return false;
        }

        if (input.Port is not int port || port is < 1 or > 65535)
        {
            error = "Port must be between 1 and 65535.";
            return false;
        }

        var directory = input.RemoteDirectory?.Trim() ?? string.Empty;
        if (!IsRemotePath(directory))
        {
            error = "Remote directory is required. Use / for the root directory.";
            return false;
        }

        if (!TryClock(input.Timeout, out var timeout) || timeout < TimeSpan.Zero || timeout > TimeSpan.FromMinutes(MaxTimeoutMinutes))
        {
            error = $"Timeout is required. Use HH:MM:SS between 00:00:00 and 00:{MaxTimeoutMinutes:00}:00.";
            return false;
        }

        if (!TryAcquisitions(input.Acquisitions, out var acquisitions, out error))
            return false;

        var username = input.Username?.Trim() ?? string.Empty;
        var password = input.Password ?? string.Empty;
        if (username.Length > 0 || password.Length > 0)
        {
            if (username.Length == 0 || password.Length == 0)
            {
                error = "Enter the username and password together. Leave both blank to keep the saved credentials.";
                return false;
            }

            if (username.Length > 256 || password.Length > 1024)
            {
                error = "Username must be 256 characters or fewer and password 1024 characters or fewer.";
                return false;
            }

            credentials = (username, password);
        }

        body = new Dictionary<string, object?>
        {
            ["OrganizationId"] = facilityId,
            ["Host"] = host,
            ["Port"] = port,
            ["RemoteDirectory"] = directory,
            ["Timeout"] = timeout.ToString("c", CultureInfo.InvariantCulture),
            ["RemoveAfterProcessing"] = input.RemoveAfterProcessing,
            ["EnableBenchmarking"] = input.EnableBenchmarking,
            ["AuthenticationProtocol"] = "Basic",
            ["AcquisitionConfigurations"] = acquisitions
        };
        if (Guid.TryParse(input.ConfigurationId, out var id) && id != Guid.Empty)
            body["Id"] = id;

        error = null;
        return true;
    }

    public static List<HeaderInput> WithBlankHeader(IEnumerable<HeaderInput>? rows) =>
        WithBlank(rows, row => string.IsNullOrWhiteSpace(row.Key) && string.IsNullOrWhiteSpace(row.Value), () => new HeaderInput());

    public static List<QueryRowInput> WithBlankQuery(IEnumerable<QueryRowInput>? rows)
    {
        var list = rows?.Select(CloneQuery).ToList() ?? new List<QueryRowInput>();
        foreach (var row in list)
            row.Parameters = WithBlankParameter(row.Parameters);

        if (list.Count == 0 || !string.IsNullOrWhiteSpace(list[^1].ResourceType))
            list.Add(EmptyQueryRow());

        return list;
    }

    public static List<QueryParameterInput> WithBlankParameter(IEnumerable<QueryParameterInput>? rows) =>
        WithBlank(rows, ParameterIsBlank, () => new QueryParameterInput { ParameterType = "Variable", Variable = "PatientId" });

    public static List<ReportingMatchInput> WithBlankMatch(IEnumerable<ReportingMatchInput>? rows) =>
        WithBlank(rows, MatchIsBlank, () => new ReportingMatchInput());

    public static List<SftpAcquisitionInput> WithBlankAcquisition(IEnumerable<SftpAcquisitionInput>? rows) =>
        WithBlank(
            rows,
            row => string.IsNullOrWhiteSpace(row.AcquisitionType)
                && string.IsNullOrWhiteSpace(row.RemoteDirectory)
                && string.IsNullOrWhiteSpace(row.ProcessedDirectory)
                && string.IsNullOrWhiteSpace(row.FileNamePattern),
            () => new SftpAcquisitionInput { SubType = "None" });

    private static bool TryAuthentication(FhirQueryPanel input, out Dictionary<string, object?>? authentication, out string? error)
    {
        authentication = null;
        if (!input.AuthEnabled)
        {
            error = null;
            return true;
        }

        var authType = FhirAuthTypes.FirstOrDefault(item => string.Equals(item, input.AuthType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (authType is null)
        {
            error = "Auth type must be Basic, Epic, OAuth, or CustomHeaders.";
            return false;
        }

        var body = new Dictionary<string, object?> { ["AuthType"] = authType };
        if (authType == "Basic")
        {
            if (!Require(input.UserName, "User name secret name", out var userName, out error)
                || !Require(input.Password, "Password secret name", out var password, out error))
                return false;

            body["UserName"] = userName;
            body["Password"] = password;
        }
        else if (authType == "Epic")
        {
            if (!Require(input.AuthKey, "Key secret name", out var key, out error)
                || !Require(input.TokenUrl, "Token URL", out var tokenUrl, out error)
                || !Require(input.Audience, "Audience", out var audience, out error)
                || !Require(input.ClientId, "Client id secret name", out var clientId, out error))
                return false;

            if (!IsHttpUrl(tokenUrl!))
            {
                error = "Token URL must be an absolute http or https address.";
                return false;
            }

            body["Key"] = key;
            body["TokenUrl"] = tokenUrl;
            body["Audience"] = audience;
            body["ClientId"] = clientId;
        }
        else if (authType == "OAuth")
        {
            if (!Require(input.TokenUrl, "Token URL", out var tokenUrl, out error)
                || !Require(input.ClientId, "Client id secret name", out var clientId, out error)
                || !Require(input.ClientSecret, "Client secret name", out var clientSecret, out error)
                || !Require(input.Scope, "Scope", out var scope, out error))
                return false;

            if (!IsHttpUrl(tokenUrl!))
            {
                error = "Token URL must be an absolute http or https address.";
                return false;
            }

            body["TokenUrl"] = tokenUrl;
            body["ClientId"] = clientId;
            body["ClientSecret"] = clientSecret;
            body["Scope"] = scope;
        }
        else
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in input.CustomHeaders ?? new List<HeaderInput>())
            {
                if (row.Remove)
                    continue;

                var key = row.Key?.Trim() ?? string.Empty;
                var value = row.Value?.Trim() ?? string.Empty;
                if (key.Length == 0 && value.Length == 0)
                    continue;

                if (key.Length == 0 || value.Length == 0)
                {
                    error = "Each custom header needs a key and a secret name.";
                    return false;
                }

                if (!headers.TryAdd(key, value))
                {
                    error = $"Custom header {key} is listed more than once.";
                    return false;
                }
            }

            if (headers.Count == 0)
            {
                error = "Custom headers authentication needs at least one header.";
                return false;
            }

            body["CustomHeaders"] = headers;
        }

        authentication = body;
        error = null;
        return true;
    }

    private static bool TryQueries(
        List<QueryRowInput>? rows,
        string label,
        bool requireUniqueResources,
        out Dictionary<string, object?> queries,
        out string? error)
    {
        queries = new Dictionary<string, object?>();
        var resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows ?? new List<QueryRowInput>())
        {
            if (row.Remove || string.IsNullOrWhiteSpace(row.ResourceType))
                continue;

            var resource = row.ResourceType.Trim();
            if (resource.Length > 100)
            {
                error = $"{label} query resource type must be 100 characters or fewer.";
                return false;
            }

            var configType = QueryConfigTypes.FirstOrDefault(item =>
                string.Equals(item, row.QueryConfigType?.Trim(), StringComparison.OrdinalIgnoreCase));
            var operation = OperationTypes.FirstOrDefault(item =>
                string.Equals(item, row.OperationType?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (configType is null || operation is null)
            {
                error = $"{label} query {resource} needs a type of Parameter or Reference and an operation of Read, Search, or SearchPost.";
                return false;
            }

            if (requireUniqueResources && !resources.Add(resource))
            {
                error = $"{label} queries use resource type {resource} more than once.";
                return false;
            }

            var query = new Dictionary<string, object?>
            {
                ["QueryConfigType"] = configType,
                ["ResourceType"] = resource,
                ["OperationType"] = operation
            };

            if (configType == "Reference")
            {
                var paged = row.Paged is > 0 ? row.Paged.Value : 100;
                query["Paged"] = paged;
            }
            else if (!TryParameters(row.Parameters, resource, out var parameters, out error))
            {
                return false;
            }
            else
            {
                query["Parameters"] = parameters;
            }

            queries[queries.Count.ToString(CultureInfo.InvariantCulture)] = query;
        }

        if (queries.Count == 0)
        {
            error = $"{label} queries need at least one resource type.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParameters(
        List<QueryParameterInput>? rows,
        string resource,
        out List<Dictionary<string, object?>> parameters,
        out string? error)
    {
        parameters = new List<Dictionary<string, object?>>();
        foreach (var row in rows ?? new List<QueryParameterInput>())
        {
            if (row.Remove || ParameterIsBlank(row))
                continue;

            var name = row.Name?.Trim() ?? string.Empty;
            var parameterType = ParameterTypes.FirstOrDefault(item =>
                string.Equals(item, row.ParameterType?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (name.Length == 0 || parameterType is null)
            {
                error = $"Each parameter on {resource} needs a name and a type.";
                return false;
            }

            var parameter = new Dictionary<string, object?>
            {
                ["ParameterType"] = parameterType,
                ["Name"] = name
            };

            if (parameterType == "Literal")
            {
                if (!Require(row.Literal, $"Literal for {name}", out var literal, out error))
                    return false;
                parameter["Literal"] = literal;
            }
            else if (parameterType == "Variable")
            {
                var variable = Variables.FirstOrDefault(item =>
                    string.Equals(item, row.Variable?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (variable is null)
                {
                    error = $"Variable for {name} must be PatientId, LookbackStart, PeriodStart, or PeriodEnd.";
                    return false;
                }

                parameter["Variable"] = variable;
                if (!string.IsNullOrWhiteSpace(row.Format))
                    parameter["Format"] = row.Format.Trim();
            }
            else
            {
                if (!Require(row.Resource, $"Resource for {name}", out var parameterResource, out error))
                    return false;
                parameter["Resource"] = parameterResource;
                parameter["Paged"] = string.IsNullOrWhiteSpace(row.Paged) ? "100" : row.Paged.Trim();
            }

            parameters.Add(parameter);
        }

        error = null;
        return true;
    }

    private static bool TryMatches(List<ReportingMatchInput>? rows, string method, out string path, out string? error)
    {
        var parts = new List<string>();
        foreach (var row in rows ?? new List<ReportingMatchInput>())
        {
            if (row.Remove || MatchIsBlank(row))
                continue;

            if (method == "identifier")
            {
                if (!Require(row.IdentifierSystem, "Identifier system", out var system, out error)
                    || !Require(row.IdentifierCode, "Identifier code", out var code, out error))
                {
                    path = string.Empty;
                    return false;
                }

                parts.Add($"Location.identifier.exists(system = '{Escape(system!)}' and value = '{Escape(code!)}')");
            }
            else if (method == "managingOrg")
            {
                if (!Require(row.OrganizationId, "Organization id", out var orgId, out error))
                {
                    path = string.Empty;
                    return false;
                }

                parts.Add($"Location.managingOrganization.reference = 'Organization/{Escape(orgId!)}'");
            }
            else
            {
                if (!Require(row.LocationTypeCode, "Location type code", out var typeCode, out error))
                {
                    path = string.Empty;
                    return false;
                }

                var built = $"Location.type.coding.exists(code = '{Escape(typeCode!)}')";
                if (!string.IsNullOrWhiteSpace(row.LocationAlias))
                    built += $" and Location.alias = '{Escape(row.LocationAlias.Trim())}'";
                parts.Add(built);
            }
        }

        if (parts.Count == 0)
        {
            path = string.Empty;
            error = "Add at least one match.";
            return false;
        }

        path = parts.Count == 1 ? parts[0] : string.Join(" or ", parts.Select(part => $"({part})"));
        error = null;
        return true;
    }

    private static bool TryAcquisitions(
        List<SftpAcquisitionInput>? rows,
        out List<Dictionary<string, object?>> acquisitions,
        out string? error)
    {
        acquisitions = new List<Dictionary<string, object?>>();
        foreach (var row in rows ?? new List<SftpAcquisitionInput>())
        {
            var blank = string.IsNullOrWhiteSpace(row.AcquisitionType)
                && string.IsNullOrWhiteSpace(row.RemoteDirectory)
                && string.IsNullOrWhiteSpace(row.ProcessedDirectory)
                && string.IsNullOrWhiteSpace(row.FileNamePattern);
            if (row.Remove || blank)
                continue;

            var type = AcquisitionTypes.FirstOrDefault(item =>
                string.Equals(item, row.AcquisitionType?.Trim(), StringComparison.OrdinalIgnoreCase));
            var subType = AcquisitionSubTypes.FirstOrDefault(item =>
                string.Equals(item, string.IsNullOrWhiteSpace(row.SubType) ? "None" : row.SubType.Trim(), StringComparison.OrdinalIgnoreCase));
            if (type is null || subType is null)
            {
                error = "Acquisition type must be Census or Resources, and sub type None or CernerCCLExtract.";
                return false;
            }

            var item = new Dictionary<string, object?>
            {
                ["AcquisitionType"] = type,
                ["SubType"] = subType
            };
            if (!TryOptionalPath(row.RemoteDirectory, "Acquisition remote directory", out var remote, out error)
                || !TryOptionalPath(row.ProcessedDirectory, "Processed directory", out var processed, out error))
                return false;

            if (remote is not null)
                item["RemoteDirectory"] = remote;
            if (processed is not null)
                item["ProcessedDirectory"] = processed;
            if (!string.IsNullOrWhiteSpace(row.FileNamePattern))
                item["FileNamePattern"] = row.FileNamePattern.Trim();

            acquisitions.Add(item);
        }

        error = null;
        return true;
    }

    private static bool TryOptionalPath(string? value, string label, out string? path, out string? error)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = null;
            return true;
        }

        path = value.Trim();
        if (!IsRemotePath(path))
        {
            error = $"{label} contains characters that are not allowed, or is blank. Leave it empty to inherit the connection directory.";
            return false;
        }

        error = null;
        return true;
    }

    public const string OvernightWindowHint = "Overnight window (crosses midnight)";

    /// <summary>
    /// A min later than max is a midnight-crossing window. The acquisition service already accepts it.
    /// </summary>
    public static string? PullWindowHint(string? minText, string? maxText)
    {
        if (!TryClock(minText, out var minSpan) || !TryClock(maxText, out var maxSpan) || minSpan <= maxSpan)
            return null;

        return OvernightWindowHint;
    }

    /// <summary>
    /// Secret names are not echoed. A blank post keeps the name already stored on the query.
    /// </summary>
    public static void KeepBlankSecrets(FhirQueryPanel? saved, FhirQueryPanel posted)
    {
        if (saved is null)
            return;

        posted.UserName = KeepSecret(posted.UserName, saved.UserName);
        posted.Password = KeepSecret(posted.Password, saved.Password);
        posted.AuthKey = KeepSecret(posted.AuthKey, saved.AuthKey);
        posted.ClientId = KeepSecret(posted.ClientId, saved.ClientId);
        posted.ClientSecret = KeepSecret(posted.ClientSecret, saved.ClientSecret);

        if (posted.CustomHeaders is null || saved.CustomHeaders is null)
            return;

        var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in saved.CustomHeaders)
        {
            var key = row.Key?.Trim();
            var value = row.Value?.Trim();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                continue;
            previous.TryAdd(key, value);
        }

        foreach (var row in posted.CustomHeaders)
        {
            if (row.Remove || string.IsNullOrWhiteSpace(row.Key) || !string.IsNullOrWhiteSpace(row.Value))
                continue;
            if (previous.TryGetValue(row.Key.Trim(), out var value))
                row.Value = value;
        }
    }

    private static string? KeepSecret(string? posted, string? saved) =>
        string.IsNullOrWhiteSpace(posted) ? saved : posted;

    private static bool TryPullPair(string? minText, string? maxText, out string? min, out string? max, out string? error)
    {
        var minBlank = string.IsNullOrWhiteSpace(minText);
        var maxBlank = string.IsNullOrWhiteSpace(maxText);
        if (minBlank && maxBlank)
        {
            min = null;
            max = null;
            error = null;
            return true;
        }

        if (minBlank || maxBlank || !TryClock(minText, out var minSpan) || !TryClock(maxText, out var maxSpan))
        {
            min = null;
            max = null;
            error = "Enter both min and max acquisition pull times as HH:MM:SS, or leave both empty.";
            return false;
        }

        min = minSpan.ToString("c", CultureInfo.InvariantCulture);
        max = maxSpan.ToString("c", CultureInfo.InvariantCulture);
        error = null;
        return true;
    }

    private static bool TryClock(string? text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (TimeSpan.TryParse(text.Trim(), CultureInfo.InvariantCulture, out value))
            return true;

        try
        {
            value = XmlConvert.ToTimeSpan(text.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void ApplyPath(ReportingOrgPanel panel, string path)
    {
        panel.FhirPath = path;
        var parts = SplitOr(path);
        var parsed = parts.Select(ParseOnePath).ToList();
        if (parsed.Count == 0 || parsed.Any(item => item is null) || parsed.Select(item => item!.Value.Method).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            panel.SetupMethod = "manual";
            panel.Matches = WithBlankMatch(null);
            return;
        }

        panel.SetupMethod = parsed[0]!.Value.Method;
        panel.Matches = WithBlankMatch(parsed.Select(item => item!.Value.Match));
    }

    private static string JoinPaths(IEnumerable<string?>? paths)
    {
        var raw = paths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!.Trim())
            .ToList() ?? new List<string>();
        if (raw.Count <= 1)
            return raw.FirstOrDefault() ?? string.Empty;

        return string.Join(" or ", raw.Select(path => $"({path})"));
    }

    private static List<string> SplitOr(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new List<string>();

        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = false;
        for (var i = 0; i < path.Length; i++)
        {
            var ch = path[i];
            if (ch == '\'' && (i == 0 || path[i - 1] != '\\'))
                quote = !quote;
            if (quote)
                continue;

            if (ch == '(')
                depth++;
            else if (ch == ')')
                depth = Math.Max(0, depth - 1);
            else if (depth == 0 && i + 4 <= path.Length && string.Equals(path.Substring(i, 4), " or ", StringComparison.Ordinal))
            {
                parts.Add(Unwrap(path[start..i]));
                i += 3;
                start = i + 1;
            }
        }

        parts.Add(Unwrap(path[start..]));
        return parts.Where(part => part.Length > 0).ToList();
    }

    private static string Unwrap(string value)
    {
        var text = value.Trim();
        if (text.Length >= 2 && text[0] == '(' && text[^1] == ')')
            return text[1..^1].Trim();
        return text;
    }

    private static (string Method, ReportingMatchInput Match)? ParseOnePath(string path)
    {
        const string identifierPrefix = "Location.identifier.exists(system = '";
        const string identifierMiddle = "' and value = '";
        const string identifierSuffix = "')";
        if (path.StartsWith(identifierPrefix, StringComparison.Ordinal) && path.EndsWith(identifierSuffix, StringComparison.Ordinal))
        {
            var inner = path[identifierPrefix.Length..^identifierSuffix.Length];
            var split = inner.IndexOf(identifierMiddle, StringComparison.Ordinal);
            if (split > 0)
            {
                return ("identifier", new ReportingMatchInput
                {
                    IdentifierSystem = Unescape(inner[..split]),
                    IdentifierCode = Unescape(inner[(split + identifierMiddle.Length)..])
                });
            }
        }

        const string orgPrefix = "Location.managingOrganization.reference = 'Organization/";
        if (path.StartsWith(orgPrefix, StringComparison.Ordinal) && path.EndsWith("'", StringComparison.Ordinal))
        {
            return ("managingOrg", new ReportingMatchInput
            {
                OrganizationId = Unescape(path[orgPrefix.Length..^1])
            });
        }

        const string typePrefix = "Location.type.coding.exists(code = '";
        if (path.StartsWith(typePrefix, StringComparison.Ordinal))
        {
            var codeEnd = path.IndexOf("')", typePrefix.Length, StringComparison.Ordinal);
            if (codeEnd > typePrefix.Length)
            {
                var code = Unescape(path[typePrefix.Length..codeEnd]);
                var alias = string.Empty;
                const string aliasMarker = " and Location.alias = '";
                var aliasAt = path.IndexOf(aliasMarker, codeEnd, StringComparison.Ordinal);
                if (aliasAt > 0 && path.EndsWith("'", StringComparison.Ordinal))
                    alias = Unescape(path[(aliasAt + aliasMarker.Length)..^1]);

                return ("locationType", new ReportingMatchInput
                {
                    LocationTypeCode = code,
                    LocationAlias = alias
                });
            }
        }

        return null;
    }

    private static List<QueryRowInput> ReadQueries(JsonElement root, string name)
    {
        var rows = new List<QueryRowInput>();
        if (!TryProp(root, name, out var queries) || queries.ValueKind != JsonValueKind.Object)
            return WithBlankQuery(rows);

        foreach (var property in queries.EnumerateObject().OrderBy(item => int.TryParse(item.Name, out var key) ? key : int.MaxValue))
        {
            var item = property.Value;
            var row = new QueryRowInput
            {
                ResourceType = StringOf(item, "resourceType"),
                QueryConfigType = StringOf(item, "queryConfigType") ?? "Parameter",
                OperationType = StringOf(item, "operationType") ?? "Search",
                Paged = IntOf(item, "paged") ?? 100,
                Parameters = ReadParameters(item)
            };
            rows.Add(row);
        }

        return WithBlankQuery(rows);
    }

    private static List<QueryParameterInput> ReadParameters(JsonElement query)
    {
        var rows = new List<QueryParameterInput>();
        if (!TryProp(query, "parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
            return rows;

        foreach (var item in parameters.EnumerateArray())
        {
            rows.Add(new QueryParameterInput
            {
                Name = StringOf(item, "name"),
                ParameterType = StringOf(item, "parameterType") ?? "Variable",
                Literal = StringOf(item, "literal"),
                Variable = StringOf(item, "variable") ?? "PatientId",
                Format = StringOf(item, "format"),
                Resource = StringOf(item, "resource"),
                Paged = StringOf(item, "paged")
            });
        }

        return rows;
    }

    private static List<SftpAcquisitionInput> ReadAcquisitions(JsonElement root)
    {
        var rows = new List<SftpAcquisitionInput>();
        if (!TryProp(root, "acquisitionConfigurations", out var items) || items.ValueKind != JsonValueKind.Array)
            return WithBlankAcquisition(rows);

        foreach (var item in items.EnumerateArray())
        {
            rows.Add(new SftpAcquisitionInput
            {
                AcquisitionType = StringOf(item, "acquisitionType"),
                SubType = StringOf(item, "subType") ?? "None",
                RemoteDirectory = StringOf(item, "remoteDirectory"),
                ProcessedDirectory = StringOf(item, "processedDirectory"),
                FileNamePattern = StringOf(item, "fileNamePattern")
            });
        }

        return WithBlankAcquisition(rows);
    }

    private static List<HeaderInput> ReadHeaders(JsonElement auth)
    {
        var rows = new List<HeaderInput>();
        if (!TryProp(auth, "customHeaders", out var headers) || headers.ValueKind != JsonValueKind.Object)
            return rows;

        foreach (var property in headers.EnumerateObject())
        {
            rows.Add(new HeaderInput
            {
                Key = property.Name,
                Value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString()
            });
        }

        return rows;
    }

    private static QueryRowInput EmptyQueryRow() => new()
    {
        QueryConfigType = "Parameter",
        OperationType = "Search",
        Paged = 100,
        Parameters = WithBlankParameter(null)
    };

    private static QueryRowInput CloneQuery(QueryRowInput row) => new()
    {
        ResourceType = row.ResourceType,
        QueryConfigType = row.QueryConfigType,
        OperationType = row.OperationType,
        Paged = row.Paged,
        Remove = row.Remove,
        Parameters = row.Parameters ?? new List<QueryParameterInput>()
    };

    private static bool ParameterIsBlank(QueryParameterInput row) =>
        string.IsNullOrWhiteSpace(row.Name)
        && string.IsNullOrWhiteSpace(row.Literal)
        && string.IsNullOrWhiteSpace(row.Format)
        && string.IsNullOrWhiteSpace(row.Resource)
        && string.IsNullOrWhiteSpace(row.Paged);

    private static bool MatchIsBlank(ReportingMatchInput row) =>
        string.IsNullOrWhiteSpace(row.IdentifierSystem)
        && string.IsNullOrWhiteSpace(row.IdentifierCode)
        && string.IsNullOrWhiteSpace(row.OrganizationId)
        && string.IsNullOrWhiteSpace(row.LocationTypeCode)
        && string.IsNullOrWhiteSpace(row.LocationAlias);

    private static List<T> WithBlank<T>(IEnumerable<T>? rows, Func<T, bool> isBlank, Func<T> create)
    {
        var list = rows?.ToList() ?? new List<T>();
        if (list.Count == 0 || !isBlank(list[^1]))
            list.Add(create());
        return list;
    }

    private static bool Require(string? value, string label, out string? text, out string? error)
    {
        text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            error = $"{label} is required.";
            text = null;
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static bool IsHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > MaxHostLength)
            return false;
        if (host.Any(ch => char.IsWhiteSpace(ch) || ch is '/' or '\\'))
            return false;
        return !host.StartsWith('.') && !host.StartsWith('-') && !host.EndsWith('.') && !host.EndsWith('-');
    }

    private static bool IsRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength)
            return false;
        return path.IndexOfAny(['<', '>', '"', '|', '?', '*', '\0']) < 0;
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private static string Unescape(string value) =>
        value.Replace("\\'", "'", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);

    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? StringOf(JsonElement element, string name)
    {
        if (!TryProp(element, name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? IntOf(JsonElement element, string name)
    {
        if (!TryProp(element, name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            return parsed;
        return null;
    }

    private static bool BoolOf(JsonElement element, string name)
    {
        if (!TryProp(element, name, out var value))
            return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static string? TimeOf(JsonElement element, string name)
    {
        var text = StringOf(element, name);
        return TryClock(text, out var value) ? value.ToString("c", CultureInfo.InvariantCulture) : text;
    }

    private static object? ToPlain(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ToPlain(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToPlain).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var whole) ? whole : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
