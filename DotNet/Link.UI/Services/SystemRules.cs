using System.Globalization;
using System.Text.Json;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Checks for the system pages. Account writes keep claims that were already stored.
/// Restore and claim assignment are not part of these checks.
/// </summary>
public static class SystemRules
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    public const int MaxRoles = 20;
    public const int MaxReportTypes = 20;
    public const int MaxPatients = 50;
    public const int MaxDelayMinutes = 10080;
    public const int MaxDescription = 1000;
    public const int MaxShownEntries = 8;
    public const int MaxRows = 40;

    public static readonly string[] HealthServices =
    [
        "account", "adminbff", "audit", "census", "dataacquisition", "measureeval", "measureevaluation",
        "normalization", "querydispatch", "report", "submission", "tenant", "terminology", "validation"
    ];

    public static readonly string[] Frequencies = ["Discharge", "Daily", "Weekly", "Monthly", "Adhoc"];
    public static readonly string[] ListTypes = ["Admit", "Discharge"];
    public static readonly string[] TimeFrames = ["LessThan24Hours", "Between24To48Hours", "MoreThan48Hours"];

    private static readonly string[] AddressLabels =
    [
        "Account", "Audit", "Census", "Data acquisition", "MeasureEval", "Mock DMRP", "Normalization",
        "Notification", "Admin.BFF", "Query dispatch", "Report", "Submission", "Tenant", "Terminology", "Validation"
    ];

    public static string? CheckUserQuery(UserQuery? query, bool numericOnly, out UserQuery value)
    {
        query ??= new UserQuery();
        value = new UserQuery
        {
            IncludeDeactivated = query.IncludeDeactivated,
            IncludeDeleted = query.IncludeDeleted,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize
        };

        var searchError = OptionalText(query.SearchText, "Search", out var search);
        if (searchError is not null)
            return searchError;
        value.SearchText = search;

        if (!string.IsNullOrWhiteSpace(query.FacilityId))
        {
            var facilityError = ConfigurationRules.CheckFacility(query.FacilityId, numericOnly, required: true, out var facility);
            if (facilityError is not null)
                return facilityError;
            value.FacilityId = facility;
        }

        var roleError = OptionalText(query.Role, "Role", out var role);
        if (roleError is not null)
            return roleError;
        value.Role = role;

        var claimError = OptionalText(query.Claim, "Claim", out var claim);
        if (claimError is not null)
            return claimError;
        value.Claim = claim;
        return null;
    }

    public static string? CheckUser(UserForm? form, IReadOnlyCollection<string> knownRoles, out AccountUserInput value)
    {
        form ??= new UserForm();
        value = new AccountUserInput();

        var usernameError = RequiredToken(form.Username, "Username", out var username);
        if (usernameError is not null)
            return usernameError;
        var firstError = RequiredName(form.FirstName, "First name", out var first);
        if (firstError is not null)
            return firstError;
        var middleError = OptionalName(form.MiddleName, "Middle name", out var middle);
        if (middleError is not null)
            return middleError;
        var lastError = RequiredName(form.LastName, "Last name", out var last);
        if (lastError is not null)
            return lastError;

        var emailError = CheckEmail(form.Email, out var email);
        if (emailError is not null)
            return emailError;

        var rolesError = CheckRoles(form.Roles, knownRoles, out var roles);
        if (rolesError is not null)
            return rolesError;

        value = new AccountUserInput
        {
            Username = username,
            FirstName = first,
            MiddleName = middle,
            LastName = last,
            Email = email,
            Roles = roles
        };
        return null;
    }

    public static string? CheckUserId(string? id, out Guid value)
    {
        value = Guid.Empty;
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id.Trim(), out value) || value == Guid.Empty)
            return "User id must be a GUID.";
        return null;
    }

    public static string? CheckRole(RoleForm? form, out string name, out string description)
    {
        form ??= new RoleForm();
        description = "";
        var nameError = RequiredName(form.Name, "Name", out name);
        if (nameError is not null)
            return nameError;
        var descriptionError = OptionalText(form.Description, "Description", MaxDescription, out var descriptionValue);
        description = descriptionValue ?? "";
        return descriptionError;
    }

    public static string? CheckRoleId(string? id, out Guid value)
    {
        value = Guid.Empty;
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id.Trim(), out value) || value == Guid.Empty)
            return "Role id must be a GUID.";
        return null;
    }

    public static AccountUserApiModel MergeUser(AccountUserApiModel existing, AccountUserInput input) => new()
    {
        Id = existing.Id,
        Username = input.Username,
        FirstName = input.FirstName,
        MiddleName = input.MiddleName,
        LastName = input.LastName,
        Email = input.Email,
        Roles = input.Roles.ToList(),
        UserClaims = existing.UserClaims ?? [],
        IsActive = existing.IsActive,
        IsDeleted = existing.IsDeleted
    };

    public static AccountRoleApiModel MergeRole(AccountRoleApiModel existing, string name, string description) => new()
    {
        Id = existing.Id,
        Name = name,
        Description = description,
        Claims = existing.Claims ?? []
    };

    public static string? CheckHealthService(string? service, out string? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(service))
            return null;
        var normalized = new string(service.Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray())
            .ToLowerInvariant();
        if (!HealthServices.Contains(normalized, StringComparer.Ordinal))
            return "Service must be one of the system health names, such as account or measureeval.";
        key = normalized;
        return null;
    }

    public static string? ReadHealth(string? json, out IReadOnlyList<HealthRow> rows)
    {
        rows = Array.Empty<HealthRow>();
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var parsed = new List<HealthRow>();
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                parsed.Add(ReadHealthRow(document.RootElement));
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (parsed.Count >= MaxRows)
                        break;
                    if (item.ValueKind == JsonValueKind.Object)
                        parsed.Add(ReadHealthRow(item));
                }
            }
            else
            {
                return "Health response was not a list.";
            }

            rows = parsed;
            return null;
        }
        catch (JsonException)
        {
            return "Health response was not valid JSON.";
        }
    }

    public static string? ReadServiceInfo(string? json, out IReadOnlyList<ServiceInfoRow> rows)
    {
        rows = Array.Empty<ServiceInfoRow>();
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return "Service information was not a list.";
            var parsed = new List<ServiceInfoRow>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (parsed.Count >= MaxRows)
                    break;
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                parsed.Add(new ServiceInfoRow
                {
                    ServiceName = ReadString(item, "serviceName"),
                    Version = ReadString(item, "version"),
                    ProductVersion = ReadString(item, "productVersion"),
                    Commit = Trim(ReadString(item, "commit"), 40),
                    Build = Trim(ReadString(item, "build"), 40)
                });
            }

            rows = parsed;
            return null;
        }
        catch (JsonException)
        {
            return "Service information was not valid JSON.";
        }
    }

    public static (IReadOnlyList<ConfiguredAddress> Rows, string Missing) ReadAddresses(ServiceRegistry? registry)
    {
        registry ??= new ServiceRegistry();
        var values = new string?[]
        {
            registry.AccountServiceUrl,
            registry.AuditServiceUrl,
            registry.CensusServiceUrl,
            registry.DataAcquisitionServiceUrl,
            registry.MeasureServiceUrl,
            registry.MockDmrpApiUrl,
            registry.NormalizationServiceUrl,
            registry.NotificationServiceUrl,
            registry.AdminBffServiceUrl,
            registry.QueryDispatchServiceUrl,
            registry.ReportServiceUrl,
            registry.SubmissionServiceUrl,
            registry.TenantService?.TenantServiceUrl,
            registry.TerminologyServiceUrl,
            registry.ValidationServiceUrl
        };

        var rows = new List<ConfiguredAddress>();
        var missing = new List<string>();
        for (var index = 0; index < AddressLabels.Length; index++)
        {
            var shown = ShowAddress(values[index]);
            if (shown is null)
                missing.Add(AddressLabels[index]);
            else
                rows.Add(new ConfiguredAddress { Label = AddressLabels[index], Url = shown });
        }

        return (rows, missing.Count == 0 ? "" : "Not set: " + string.Join(", ", missing) + ".");
    }

    public static string? CheckReportScheduled(
        ReportScheduledForm? form,
        bool numericOnly,
        DateTime utcNow,
        out ReportScheduledRequest? request)
    {
        form ??= new ReportScheduledForm();
        request = null;
        var facilityError = ConfigurationRules.CheckFacility(form.FacilityId, numericOnly, required: true, out var facility);
        if (facilityError is not null)
            return facilityError;

        var frequency = form.Frequency?.Trim() ?? "";
        if (!Frequencies.Contains(frequency, StringComparer.Ordinal))
            return "Frequency must be Discharge, Daily, Weekly, Monthly, or Adhoc.";

        var typesError = SplitTokens(form.ReportTypes, "report type", MaxReportTypes, out var types);
        if (typesError is not null)
            return typesError;
        if (types.Count == 0)
            return "Enter at least one report type.";

        if (!DateTime.TryParseExact(form.StartDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return "Start date must be a calendar date.";
        var start = DateTime.SpecifyKind(day, DateTimeKind.Utc);
        if (start >= utcNow)
            return "Start date must be in the past.";

        if (!int.TryParse(form.DelayMinutes?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var delay)
            || delay is < 0 or > MaxDelayMinutes)
            return "Delay must be a whole number of minutes from 0 through 10080.";

        var trackingError = CheckTrackingId(form.ReportTrackingId, out var tracking);
        if (trackingError is not null)
            return trackingError;

        request = new ReportScheduledRequest
        {
            FacilityId = facility ?? "",
            Frequency = frequency,
            ReportTypes = types,
            StartDateUtc = start,
            DelayMinutes = delay,
            ReportTrackingId = tracking
        };
        return null;
    }

    public static string? CheckPatientList(PatientListForm? form, bool numericOnly, out PatientListRequest? request)
    {
        form ??= new PatientListForm();
        request = null;
        var facilityError = ConfigurationRules.CheckFacility(form.FacilityId, numericOnly, required: true, out var facility);
        if (facilityError is not null)
            return facilityError;

        var listType = form.ListType?.Trim() ?? "";
        if (!ListTypes.Contains(listType, StringComparer.Ordinal))
            return "List type must be Admit or Discharge.";
        var timeFrame = form.TimeFrame?.Trim() ?? "";
        if (!TimeFrames.Contains(timeFrame, StringComparer.Ordinal))
            return "Time frame must be LessThan24Hours, Between24To48Hours, or MoreThan48Hours.";

        var patientsError = SplitTokens(form.PatientIds, "patient id", MaxPatients, out var patients);
        if (patientsError is not null)
            return patientsError;
        if (patients.Count == 0)
            return "Enter at least one patient id.";

        var trackingError = CheckTrackingId(form.ReportTrackingId, out var tracking);
        if (trackingError is not null)
            return trackingError;

        request = new PatientListRequest
        {
            FacilityId = facility ?? "",
            ListType = listType,
            TimeFrame = timeFrame,
            PatientIds = patients,
            ReportTrackingId = tracking
        };
        return null;
    }

    public static string FormatDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        if (!TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var span))
            return value.Trim();
        var seconds = span.TotalSeconds;
        if (seconds == 0)
            return "0 ms";
        if (seconds < 0.001)
            return "<1 ms";
        if (seconds < 1)
            return Math.Round(seconds * 1000).ToString(CultureInfo.InvariantCulture) + " ms";
        if (seconds < 60)
            return Math.Round(seconds, 2).ToString(CultureInfo.InvariantCulture) + " s";
        return value.Trim();
    }

    private static readonly System.Text.RegularExpressions.Regex EmailPattern = new(
        "^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string? CheckEmail(string? value, out string email)
    {
        email = value?.Trim() ?? "";
        if (email.Length == 0)
            return "Email is required.";
        if (email.Length > ConfigurationRules.MaxText || !EmailPattern.IsMatch(email))
            return "Email is not valid.";
        return null;
    }

    private static string? CheckRoles(IEnumerable<string>? posted, IReadOnlyCollection<string> knownRoles, out IReadOnlyList<string> roles)
    {
        roles = Array.Empty<string>();
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in knownRoles)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            known.TryAdd(name.Trim(), name.Trim());
        }

        var selected = new List<string>();
        foreach (var role in posted ?? Array.Empty<string>())
        {
            var trimmed = role?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            if (!known.TryGetValue(trimmed, out var canonical))
                return "Each role must be a role that already exists.";
            if (selected.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                continue;
            if (selected.Count >= MaxRoles)
                return "An account can have at most 20 roles.";
            selected.Add(canonical);
        }

        roles = selected;
        return null;
    }

    private static string? CheckTrackingId(string? value, out Guid? tracking)
    {
        tracking = null;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!Guid.TryParse(value.Trim(), out var parsed) || parsed == Guid.Empty)
            return "Report tracking id must be a GUID, or left blank.";
        tracking = parsed;
        return null;
    }

    private static string? SplitTokens(string? raw, string label, int max, out IReadOnlyList<string> tokens)
    {
        tokens = Array.Empty<string>();
        var parsed = new List<string>();
        foreach (var part in (raw ?? "").Split([',', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim();
            if (token.Length is < 1 or > ConfigurationRules.MaxText || !IsToken(token))
                return "Each " + label + " must be letters, numbers, dots, hyphens, or underscores.";
            if (parsed.Contains(token, StringComparer.Ordinal))
                continue;
            if (parsed.Count >= max)
                return "Enter at most " + max + " " + label + "s.";
            parsed.Add(token);
        }

        tokens = parsed;
        return null;
    }

    private static HealthRow ReadHealthRow(JsonElement item)
    {
        var entries = ReadEntries(item);
        return new HealthRow
        {
            Service = ReadString(item, "service"),
            Status = ReadString(item, "status"),
            Duration = FormatDuration(ReadString(item, "totalDuration")),
            Entries = entries.Take(MaxShownEntries).ToArray(),
            HiddenEntries = Math.Max(0, entries.Count - MaxShownEntries)
        };
    }

    private static List<HealthEntryRow> ReadEntries(JsonElement item)
    {
        var rows = new List<HealthEntryRow>();
        if (!item.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Object)
            return rows;
        foreach (var entry in entries.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
                continue;
            rows.Add(new HealthEntryRow
            {
                Name = Trim(entry.Name, ConfigurationRules.MaxText),
                Status = ReadString(entry.Value, "status"),
                Duration = FormatDuration(ReadString(entry.Value, "duration")),
                Description = Trim(ReadString(entry.Value, "description"), 200)
            });
        }

        return rows;
    }

    private static string ReadString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var property))
            return "";
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? "",
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };
    }

    private static string? ShowAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var builder = new UriBuilder(uri) { UserName = "", Password = "", Query = "" };
            return builder.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped)
                .TrimEnd('/');
        }

        if (trimmed.Contains("password", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains('@'))
            return null;
        return Trim(trimmed, 300);
    }

    private static string? OptionalText(string? value, string label, out string? cleaned) =>
        OptionalText(value, label, ConfigurationRules.MaxText, out cleaned);

    private static string? OptionalText(string? value, string label, int max, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (cleaned is null)
            return null;
        if (cleaned.Length > max || HasMarkup(cleaned))
        {
            cleaned = null;
            return label + " must be " + max + " characters or fewer.";
        }

        return null;
    }

    private static string? RequiredName(string? value, string label, out string cleaned)
    {
        cleaned = "";
        var error = OptionalName(value, label, out var optional);
        if (error is not null)
            return error;
        if (optional is null)
            return label + " is required.";
        cleaned = optional;
        return null;
    }

    private static string? OptionalName(string? value, string label, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (cleaned is null)
            return null;
        if (cleaned.Length > ConfigurationRules.MaxText || HasMarkup(cleaned))
        {
            cleaned = null;
            return label + " must be 255 characters or fewer.";
        }

        return null;
    }

    private static string? RequiredToken(string? value, string label, out string cleaned)
    {
        cleaned = value?.Trim() ?? "";
        if (cleaned.Length is < 1 or > ConfigurationRules.MaxText || !IsToken(cleaned))
            return label + " must be letters, numbers, dots, hyphens, or underscores.";
        return null;
    }

    private static bool IsToken(string value) =>
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private static bool HasMarkup(string value) =>
        value.Any(character => character is '<' or '>' || char.IsControl(character));

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
