using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Configuration-area decisions that do not call a service: which inputs are accepted,
/// how lists are paged, and how service JSON is read for the page.
/// </summary>
public static class ConfigurationRules
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    public const int NotificationPageMax = 20;
    public const int TerminologyPageMax = 100;
    public const int HslocPageSize = 20;
    public const int MaxText = 255;
    public const int MaxUrl = 2000;
    public const int MaxGuidance = 1000;
    public const int MaxTitle = 500;
    public const int MaxEmails = 20;
    public const int MinCodeSearch = 3;
    public const int MaxUploadBytes = 8_000_000;
    public const int PreviewLength = 4000;
    public const int MaxMatcherDepth = 8;
    public const int MaxListRows = 200;
    public const int MaxBody = 4000;
    public const string ReservedCategory = "uncategorized";
    public const string EmailChannel = "Email";

    public static readonly int[] PageSizes = [10, 20, 50];
    public static readonly int[] NotificationPageSizes = [10, NotificationPageMax];
    public static readonly string[] Severities = ["ERROR", "WARNING", "INFORMATION"];
    public static readonly string[] Frequencies = ["Discharge", "Daily", "Weekly", "Monthly", "Adhoc"];
    public static readonly string[] OperationSorts = ["CreateDate", "Name", "OperationType", "FacilityId"];
    public static readonly string[] ResultFields = ["SEVERITY", "CODE", "MESSAGE", "EXPRESSION"];
    public static readonly string[] DebugSections = ["groups", "expressions", "librarydebug", "messages", "traces", "debuglog"];
    public static readonly string[] NotificationTypes = ["System Notification", "Test Notification"];

    private static readonly Regex MeasureIdPattern = new("^[A-Za-z0-9][A-Za-z0-9\\-.]{0,63}$", RegexOptions.Compiled);
    private static readonly Regex SecretPattern = new("^[0-9a-zA-Z-]{1,127}$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CategoryIdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled);
    private static readonly Regex PackageNamePattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,119}$", RegexOptions.Compiled);
    private static readonly Regex LibraryIdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled);
    private static readonly Regex CqlRangePattern = new("^\\d+:\\d+-\\d+:\\d+$", RegexOptions.Compiled);

    public static int ClampPage(int? page) => page is null or < 1 ? 1 : page.Value;

    public static int ClampPageSize(int? size, int max = MaxPageSize)
    {
        if (size is null || size < 1)
            return DefaultPageSize;
        if (size > max)
            return max;
        return size.Value;
    }

    public static int ListedPageSize(int? size, int[] allowed)
    {
        if (size is not null && allowed.Contains(size.Value))
            return size.Value;
        return allowed[0];
    }

    public static PageBar Bar(int page, int pageSize, long totalCount, int? totalPages = null)
    {
        var pages = totalPages is > 0
            ? totalPages.Value
            : (int)Math.Max(1, Math.Ceiling(totalCount / (double)Math.Max(pageSize, 1)));
        return new PageBar
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = pages
        };
    }

    public static string Cap(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= PreviewLength ? text : text[..PreviewLength] + "…";
    }

    public static bool TryHttpUrl(string? value, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        url = uri.AbsoluteUri;
        return true;
    }

    public static string? CheckMeasureId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !MeasureIdPattern.IsMatch(id.Trim()))
            return "Measure id must be 1 to 64 letters, numbers, hyphens, or dots.";
        return null;
    }

    public static string? CheckBundle(string? json, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
            return "A measure definition bundle is required.";
        if (json.Length > MaxUploadBytes)
            return "The bundle is larger than 8 MB.";

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return "The bundle must be a JSON object.";
            if (!document.RootElement.TryGetProperty("resourceType", out var type)
                || !string.Equals(type.GetString(), "Bundle", StringComparison.Ordinal))
                return "The bundle resourceType must be Bundle.";
            if (!document.RootElement.TryGetProperty("id", out var rawId) || rawId.ValueKind != JsonValueKind.String)
                return "Bundle.id is required.";
            id = rawId.GetString()?.Trim() ?? string.Empty;
            return CheckMeasureId(id);
        }
        catch (JsonException)
        {
            return "The bundle is not valid JSON.";
        }
    }

    public static IReadOnlyList<MeasureRow>? ReadMeasures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<MeasureRow>();
        if (!TryArray(json, out var items))
            return null;

        var rows = new List<MeasureRow>();
        foreach (var item in items)
        {
            var id = Text(item, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            rows.Add(new MeasureRow
            {
                Id = id,
                Version = Text(item, "version"),
                Modified = When(item, "modifiedDate")
            });
        }

        return rows;
    }

    public static void ReadMeasure(string? json, MeasureDetailPage page)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;
            var root = document.RootElement;
            page.Version = Text(root, "version");
            page.Created = When(root, "createdDate");
            page.Modified = When(root, "modifiedDate");
            page.Libraries = LibraryIds(root);
            if (root.TryGetProperty("bundle", out var bundle)
                && bundle.ValueKind == JsonValueKind.Object
                && bundle.TryGetProperty("entry", out var entries)
                && entries.ValueKind == JsonValueKind.Array)
            {
                page.EntryCount = entries.GetArrayLength();
            }
        }
        catch (JsonException)
        {
            page.LoadError ??= "The measure definition was not valid JSON.";
        }
    }

    public static IReadOnlyList<RelatedArtifactRow> ReadArtifacts(string? json)
    {
        if (!TryArray(json, out var items))
            return Array.Empty<RelatedArtifactRow>();

        var rows = new List<RelatedArtifactRow>();
        foreach (var item in items)
        {
            var name = Text(item, "name");
            var url = Text(item, "url");
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(url))
                continue;
            rows.Add(new RelatedArtifactRow
            {
                Name = string.IsNullOrWhiteSpace(name) ? url : name,
                Url = TryHttpUrl(url, out var safe) ? safe : null,
                Version = Text(item, "version")
            });
        }

        return rows;
    }

    public static IReadOnlyList<TerminologyResourceRow>? ReadTerminology(string? json, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TerminologyResourceRow>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!document.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return Array.Empty<TerminologyResourceRow>();

            var rows = new List<TerminologyResourceRow>();
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                    continue;
                var type = Text(resource, "resourceType");
                if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, resourceType, StringComparison.Ordinal))
                    continue;
                rows.Add(new TerminologyResourceRow
                {
                    ResourceType = string.IsNullOrWhiteSpace(type) ? resourceType : type,
                    Id = Text(resource, "id"),
                    Url = Text(resource, "url"),
                    Version = Text(resource, "version")
                });
            }

            return rows
                .OrderBy(row => row.Url, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? CheckName(string? name, out string value)
    {
        value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > MaxText)
            return "Name is required and must be 255 characters or fewer.";
        return null;
    }

    public static string? CheckVersion(string? version, out string value)
    {
        value = version?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > MaxText)
            return "Version is required and must be 255 characters or fewer.";
        return null;
    }

    /// <summary>A blank secret name clears the association. A value must be a Key Vault secret name.</summary>
    public static string? CheckSecret(string? secret, out string? value)
    {
        value = string.IsNullOrWhiteSpace(secret) ? null : secret.Trim();
        if (value is null)
            return null;
        if (!SecretPattern.IsMatch(value))
            return "Signing key secret id must be 1 to 127 letters, numbers, or dashes.";
        return null;
    }

    public static string? CheckCategory(CategoryForm? form, out CategoryForm value)
    {
        form ??= new CategoryForm();
        var id = form.Id?.Trim() ?? string.Empty;
        var title = form.Title?.Trim() ?? string.Empty;
        var severity = Severities.FirstOrDefault(item => string.Equals(item, form.Severity?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        var guidance = form.Guidance?.Trim() ?? string.Empty;
        value = new CategoryForm
        {
            Id = id,
            Title = title,
            Severity = severity,
            Acceptable = form.Acceptable,
            Submit = form.Submit,
            Review = form.Review,
            Guidance = guidance
        };

        if (string.Equals(id, ReservedCategory, StringComparison.OrdinalIgnoreCase))
            return "uncategorized is reserved and cannot be edited.";
        if (!CategoryIdPattern.IsMatch(id))
            return "Category id must be 1 to 64 letters, numbers, dots, underscores, or hyphens.";
        if (title.Length is < 1 or > MaxTitle)
            return "Title is required and must be 500 characters or fewer.";
        if (severity.Length == 0)
            return "Severity must be ERROR, WARNING, or INFORMATION.";
        if (guidance.Length is < 1 or > MaxGuidance)
            return "Guidance is required and must be 1000 characters or fewer.";
        return null;
    }

    public static string? CheckFacility(string? facilityId, bool numericOnly, bool required, out string? value)
    {
        value = string.IsNullOrWhiteSpace(facilityId) ? null : facilityId.Trim();
        if (value is null)
            return required ? FacilityFormRules.FacilityIdRule(numericOnly) : null;
        if (!FacilityFormRules.IsValidFacilityId(value, numericOnly))
        {
            value = null;
            return FacilityFormRules.FacilityIdRule(numericOnly);
        }

        return null;
    }

    public static string? CheckCodeSearch(CodeQuery? query, out CodeQuery value)
    {
        query ??= new CodeQuery();
        var search = query.Search?.Trim();
        var codeSystem = query.CodeSystem?.Trim();
        var valueSet = query.ValueSet?.Trim();
        var version = query.Version?.Trim();
        value = new CodeQuery
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            CodeSystem = string.IsNullOrWhiteSpace(codeSystem) ? null : codeSystem,
            ValueSet = string.IsNullOrWhiteSpace(valueSet) ? null : valueSet,
            Version = string.IsNullOrWhiteSpace(version) ? null : version,
            ExcludeInactive = query.ExcludeInactive,
            Page = ClampPage(query.Page),
            PageSize = ClampPageSize(query.PageSize, TerminologyPageMax)
        };

        if (search?.Length > MaxText || codeSystem?.Length > MaxUrl || valueSet?.Length > MaxUrl || version?.Length > MaxText)
            return "Search text and version must be 255 characters or fewer. A code system or value set must be 2000 characters or fewer.";

        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var hasSystem = !string.IsNullOrWhiteSpace(codeSystem);
        var hasSet = !string.IsNullOrWhiteSpace(valueSet);
        if (!string.IsNullOrWhiteSpace(version) && !hasSystem && !hasSet)
            return "A version needs the code system or value set it belongs to.";
        if (!hasSearch && !hasSystem && !hasSet)
            return "Enter a search of at least 3 characters, a code system, or a value set.";
        if (hasSearch && search!.Length < MinCodeSearch)
            return "Search must be at least 3 characters.";
        if (hasSystem && hasSet)
            return "Search one code system or one value set, not both.";
        return null;
    }

    public static string? CheckEmails(string? text, bool channelEnabled, out List<string> emails)
    {
        emails = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return channelEnabled ? "Add at least one email address when the email channel is on." : null;

        foreach (var part in text.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var email = part.Trim();
            if (email.Length == 0)
                continue;
            if (email.Length > MaxText || !EmailPattern.IsMatch(email))
                return "Each recipient must be an email address.";
            if (emails.Any(existing => string.Equals(existing, email, StringComparison.OrdinalIgnoreCase)))
                continue;
            emails.Add(email);
            if (emails.Count > MaxEmails)
                return "A configuration can list at most 20 email addresses.";
        }

        if (emails.Count == 0 && channelEnabled)
            return "Add at least one email address when the email channel is on.";
        return null;
    }

    public static string? CheckMapping(MappingForm? form, bool creating, out MappingForm value)
    {
        form ??= new MappingForm();
        var id = form.Id?.Trim() ?? string.Empty;
        var measure = form.Measure?.Trim() ?? string.Empty;
        var dqm = form.Dqm?.Trim() ?? string.Empty;
        var frequency = Frequencies.FirstOrDefault(item => string.Equals(item, form.Frequency?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        value = new MappingForm { Id = id, Measure = measure, Dqm = dqm, Frequency = frequency };

        if (!creating && !Guid.TryParse(id, out _))
            return "Mapping id is not a valid id.";
        if (measure.Length is < 1 or > MaxText || dqm.Length is < 1 or > MaxText)
            return "Measure and dQM are required and must be 255 characters or fewer.";
        if (frequency.Length == 0)
            return "Frequency must be Discharge, Daily, Weekly, Monthly, or Adhoc.";
        return null;
    }

    public static string? CheckMappingSearch(MappingQuery? query, out MappingQuery value)
    {
        query ??= new MappingQuery();
        var frequency = string.IsNullOrWhiteSpace(query.Frequency)
            ? null
            : Frequencies.FirstOrDefault(item => string.Equals(item, query.Frequency.Trim(), StringComparison.OrdinalIgnoreCase));
        value = new MappingQuery
        {
            Measure = Limit(query.Measure),
            Dqm = Limit(query.Dqm),
            Frequency = frequency,
            Page = ClampPage(query.Page),
            PageSize = ListedPageSize(query.PageSize, PageSizes)
        };
        if (!string.IsNullOrWhiteSpace(query.Frequency) && frequency is null)
            return "Frequency must be Discharge, Daily, Weekly, Monthly, or Adhoc.";
        return null;
    }

    public static string? CheckOperation(OperationQuery? query, bool numericOnly, out OperationQuery value)
    {
        query ??= new OperationQuery();
        var facilityError = CheckFacility(query.FacilityId, numericOnly, required: false, out var facilityId);
        var type = string.IsNullOrWhiteSpace(query.OperationType)
            ? null
            : FacilityNormalizationRules.CanonicalType(query.OperationType);
        Guid? operationId = null;
        Guid? vendorVersionId = null;
        string? idError = null;
        if (!string.IsNullOrWhiteSpace(query.OperationId))
        {
            if (Guid.TryParse(query.OperationId.Trim(), out var parsed))
                operationId = parsed;
            else
                idError = "Operation id is not a valid id.";
        }

        if (!string.IsNullOrWhiteSpace(query.VendorVersionId))
        {
            if (Guid.TryParse(query.VendorVersionId.Trim(), out var parsed))
                vendorVersionId = parsed;
            else
                idError ??= "Vendor version id is not a valid id.";
        }

        var sort = OperationSorts.FirstOrDefault(item => string.Equals(item, query.SortBy?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? "CreateDate";
        var dir = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        value = new OperationQuery
        {
            FacilityId = facilityId,
            OperationType = type,
            ResourceType = Limit(query.ResourceType),
            OperationId = operationId?.ToString(),
            VendorVersionId = vendorVersionId?.ToString(),
            IncludeDisabled = query.IncludeDisabled,
            SortBy = sort,
            SortDir = dir,
            Page = ClampPage(query.Page),
            PageSize = ListedPageSize(query.PageSize, PageSizes)
        };

        if (facilityError is not null)
            return facilityError;
        if (!string.IsNullOrWhiteSpace(query.OperationType) && type is null)
            return "Operation type is not one of the normalization operation types.";
        return idError;
    }

    public static string? CheckNotification(NotificationQuery? query, bool numericOnly, out NotificationQuery value)
    {
        query ??= new NotificationQuery();
        var facilityError = CheckFacility(query.FacilityId, numericOnly, required: false, out var facilityId);
        var created = CheckRange(query.CreatedOnStart, query.CreatedOnEnd, "Created");
        var sent = CheckRange(query.SentOnStart, query.SentOnEnd, "Sent");
        value = new NotificationQuery
        {
            SearchText = Limit(query.SearchText),
            FacilityId = facilityId,
            NotificationType = Limit(query.NotificationType),
            CreatedOnStart = created.Start,
            CreatedOnEnd = created.End,
            SentOnStart = sent.Start,
            SentOnEnd = sent.End,
            Page = ClampPage(query.Page),
            PageSize = ListedPageSize(query.PageSize, NotificationPageSizes)
        };
        return facilityError ?? created.Error ?? sent.Error;
    }

    public static string? CheckConfigSearch(NotificationConfigQuery? query, bool numericOnly, out NotificationConfigQuery value)
    {
        query ??= new NotificationConfigQuery();
        var facilityError = CheckFacility(query.FacilityId, numericOnly, required: false, out var facilityId);
        value = new NotificationConfigQuery
        {
            SearchText = Limit(query.SearchText),
            FacilityId = facilityId,
            Page = ClampPage(query.Page),
            PageSize = ListedPageSize(query.PageSize, NotificationPageSizes),
            Edit = Guid.TryParse(query.Edit, out var id) ? id.ToString() : null
        };
        if (!string.IsNullOrWhiteSpace(query.Edit) && value.Edit is null)
            return "Configuration id is not a valid id.";
        return facilityError;
    }

    public static string? CheckHslocVersions(string? oldVersion, string? newVersion, out string oldValue, out string newValue)
    {
        oldValue = oldVersion?.Trim() ?? string.Empty;
        newValue = newVersion?.Trim() ?? string.Empty;
        if (oldValue.Length is < 1 or > MaxText || newValue.Length is < 1 or > MaxText)
            return "Old version and new version are required and must be 255 characters or fewer.";
        return null;
    }

    public static (IReadOnlyList<HslocRow> Page, PageBar Bar) PageHsloc(IReadOnlyList<HslocRow> rows, HslocQuery? query)
    {
        query ??= new HslocQuery();
        var text = query.Text?.Trim();
        var version = query.Version?.Trim();
        IEnumerable<HslocRow> filtered = rows;
        if (!string.IsNullOrWhiteSpace(text))
        {
            filtered = filtered.Where(row =>
                row.Code.Contains(text, StringComparison.OrdinalIgnoreCase)
                || row.CdcCode.Contains(text, StringComparison.OrdinalIgnoreCase)
                || row.ShortDescription.Contains(text, StringComparison.OrdinalIgnoreCase)
                || row.LongDescription.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(version))
            filtered = filtered.Where(row => row.Version.Contains(version, StringComparison.OrdinalIgnoreCase));

        var matches = filtered.ToList();
        var page = ClampPage(query.Page);
        var size = HslocPageSize;
        var pages = Math.Max(1, (int)Math.Ceiling(matches.Count / (double)size));
        if (page > pages)
            page = pages;
        var slice = matches.Skip((page - 1) * size).Take(size).ToList();
        return (slice, Bar(page, size, matches.Count, pages));
    }

    public static string SortOrder(string? dir) =>
        string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase) ? "Ascending" : "Descending";

    public static string? CheckPackageName(string? name, out string value)
    {
        value = name?.Trim() ?? string.Empty;
        if (!PackageNamePattern.IsMatch(value))
            return "Package name must be 1 to 120 letters, numbers, dots, underscores, or hyphens.";
        return null;
    }

    /// <summary>
    /// A regex matcher is field, regex, and inverted. A composite matcher is children.
    /// A non-blank <paramref name="matcherJson"/> is sent as written after it passes the same checks.
    /// </summary>
    public static string? CheckRule(string? field, string? regex, bool inverted, string? matcherJson, out string json)
    {
        json = string.Empty;
        if (!string.IsNullOrWhiteSpace(matcherJson))
        {
            var trimmed = matcherJson.Trim();
            if (trimmed.Length > 100_000)
                return "The matcher JSON is too long.";
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return "The matcher must be a JSON object.";
                var error = CheckMatcher(document.RootElement, 0);
                if (error is not null)
                    return error;
                json = trimmed;
                return null;
            }
            catch (JsonException)
            {
                return "The matcher is not valid JSON.";
            }
        }

        var cleanField = ResultFields.FirstOrDefault(item => string.Equals(item, field?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (cleanField is null)
            return "Field must be SEVERITY, CODE, MESSAGE, or EXPRESSION.";
        var pattern = regex?.Trim() ?? string.Empty;
        if (pattern.Length is < 1 or > 500)
            return "Regex is required and must be 500 characters or fewer.";
        if (!Compiles(pattern))
            return "Regex is not a valid pattern.";

        json = JsonSerializer.Serialize(new { field = cleanField, regex = pattern, inverted });
        return null;
    }

    /// <summary>
    /// Bulk import replaces the catalog. The file must already be a JSON array the validation service can store.
    /// </summary>
    public static string? CheckBulkImport(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "A category export file is required.";
        if (json.Length > MaxUploadBytes)
            return "The file is larger than 8 MB.";

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return "The file must be a JSON array of categories.";
            var count = document.RootElement.GetArrayLength();
            if (count == 0)
                return "No categories provided.";
            if (count > 500)
                return "The file has more than 500 categories.";

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    return "Category at index " + index + " must be an object.";
                var id = Text(item, "id");
                if (string.Equals(id, ReservedCategory, StringComparison.OrdinalIgnoreCase))
                    return "uncategorized is reserved (index " + index + ").";
                if (!CategoryIdPattern.IsMatch(id))
                    return "Category id at index " + index + " is not valid.";
                if (!ids.Add(id))
                    return "Duplicate IDs provided: " + id + ".";
                var title = Text(item, "title");
                if (title.Length is < 1 or > MaxTitle)
                    return "Title at index " + index + " is required.";
                var severity = Text(item, "severity");
                if (!Severities.Contains(severity, StringComparer.Ordinal))
                    return "Severity at index " + index + " must be ERROR, WARNING, or INFORMATION.";
                var guidance = Text(item, "guidance");
                if (guidance.Length is < 1 or > MaxGuidance)
                    return "Guidance at index " + index + " is required.";
                if (!item.TryGetProperty("matcher", out var matcher) || matcher.ValueKind != JsonValueKind.Object)
                    return "No matcher provided at index " + index + ".";
                var matcherError = CheckMatcher(matcher, 0);
                if (matcherError is not null)
                    return matcherError + " at index " + index + ".";
                index++;
            }

            return null;
        }
        catch (JsonException)
        {
            return "The file is not valid JSON.";
        }
    }

    public static string? CheckCql(string? libraryId, string? range, out string library, out string? cleanRange)
    {
        library = libraryId?.Trim() ?? string.Empty;
        cleanRange = string.IsNullOrWhiteSpace(range) ? null : range.Trim();
        if (!LibraryIdPattern.IsMatch(library))
            return "Library id must be 1 to 64 letters, numbers, dots, underscores, or hyphens.";
        if (cleanRange is not null && !CqlRangePattern.IsMatch(cleanRange))
            return "Range must look like 37:1-38:22, or be left blank.";
        return null;
    }

    public static string? CheckEvaluate(string? parameters, string? debug, out string? cleanDebug)
    {
        cleanDebug = null;
        if (string.IsNullOrWhiteSpace(parameters))
            return "Parameters are required.";
        if (parameters.Length > MaxUploadBytes)
            return "The parameters are larger than 8 MB.";

        try
        {
            using var document = JsonDocument.Parse(parameters);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return "Parameters must be a JSON object.";
            if (!document.RootElement.TryGetProperty("resourceType", out var type)
                || !string.Equals(type.GetString(), "Parameters", StringComparison.Ordinal))
                return "Parameters resourceType must be Parameters.";
        }
        catch (JsonException)
        {
            return "Parameters are not valid JSON.";
        }

        if (string.IsNullOrWhiteSpace(debug) || string.Equals(debug.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            return null;

        var trimmed = debug.Trim();
        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "all", StringComparison.OrdinalIgnoreCase))
        {
            cleanDebug = trimmed.ToLowerInvariant();
            return null;
        }

        var tokens = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return "Debug must be true, all, false, or a list of sections.";
        var clean = new List<string>();
        foreach (var token in tokens)
        {
            var match = DebugSections.FirstOrDefault(item => string.Equals(item, token, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return "Debug sections are groups, expressions, librarydebug, messages, traces, and debuglog.";
            if (!clean.Contains(match, StringComparer.Ordinal))
                clean.Add(match);
        }

        cleanDebug = string.Join(",", clean);
        return null;
    }

    public static string? CheckSend(
        NotificationSendForm? form,
        bool numericOnly,
        out NotificationSendForm value,
        out List<string> recipients,
        out List<string> bcc)
    {
        form ??= new NotificationSendForm();
        recipients = new List<string>();
        bcc = new List<string>();
        var type = NotificationTypes.FirstOrDefault(item => string.Equals(item, form.NotificationType?.Trim(), StringComparison.Ordinal)) ?? string.Empty;
        var subject = form.Subject?.Trim() ?? string.Empty;
        var body = form.Body?.Trim() ?? string.Empty;
        var facilityError = CheckFacility(form.FacilityId, numericOnly, required: false, out var facility);
        value = new NotificationSendForm
        {
            NotificationType = type,
            FacilityId = facility,
            Subject = subject,
            Body = body,
            Recipients = form.Recipients,
            Bcc = form.Bcc
        };

        if (type.Length == 0)
            return "Type must be System Notification or Test Notification.";
        if (facilityError is not null)
            return facilityError;
        if (subject.Length is < 1 or > MaxText)
            return "Subject is required and must be 255 characters or fewer.";
        if (body.Length is < 1 or > MaxBody)
            return "Body is required and must be 4000 characters or fewer.";

        var recipientError = CheckEmails(form.Recipients, channelEnabled: true, out recipients);
        if (recipientError is not null)
            return MapAddressError(recipientError, "recipient", "recipients");
        var bccError = CheckEmails(form.Bcc, channelEnabled: false, out bcc);
        if (bccError is not null)
            return MapAddressError(bccError, "bcc address", "bcc addresses");
        return null;
    }

    public static IReadOnlyList<string> LibraryIds(JsonElement root)
    {
        if (!root.TryGetProperty("bundle", out var bundle) || bundle.ValueKind != JsonValueKind.Object)
            return Array.Empty<string>();
        if (!bundle.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var ids = new List<string>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                continue;
            if (!string.Equals(Text(resource, "resourceType"), "Library", StringComparison.Ordinal))
                continue;
            var url = Text(resource, "url");
            var slash = url.LastIndexOf('/');
            if (slash < 0 || slash >= url.Length - 1)
                continue;
            var id = url[(slash + 1)..];
            if (LibraryIdPattern.IsMatch(id) && !ids.Contains(id, StringComparer.Ordinal))
                ids.Add(id);
        }

        return ids;
    }

    public static void ReadPackage(string? json, PackagePage page)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                page.LoadError ??= "Package details were not an object.";
                return;
            }

            var root = document.RootElement;
            page.Version = Text(root, "version");
            page.Title = Text(root, "name");
            if (!root.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
            {
                page.Resources = Array.Empty<PackageResourceRow>();
                return;
            }

            page.Resources = resources.EnumerateArray().Select(item => new PackageResourceRow
            {
                ResourceType = Text(item, "resourceType"),
                Id = Text(item, "id"),
                Name = Text(item, "name"),
                Url = Text(item, "url"),
                Version = Text(item, "version")
            }).Take(MaxListRows).ToList();
        }
        catch (JsonException)
        {
            page.LoadError ??= "Package details were not valid JSON.";
        }
    }

    public static IReadOnlyList<DependencyRow>? ReadDependencies(string? json, out bool shortened)
    {
        shortened = false;
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<DependencyRow>();
        if (!TryArray(json, out var items))
            return null;

        var rows = new List<DependencyRow>();
        foreach (var item in items)
        {
            var sources = 0;
            if (item.TryGetProperty("sourceProfile", out var profiles) && profiles.ValueKind == JsonValueKind.Array)
                sources = profiles.GetArrayLength();
            rows.Add(new DependencyRow
            {
                Url = Text(item, "url"),
                Version = Text(item, "version"),
                ResourceExists = Flag(item, "resourceExists"),
                VersionExists = Flag(item, "versionExists"),
                SourceCount = sources
            });
        }

        if (rows.Count > MaxListRows)
        {
            shortened = true;
            return rows.Take(MaxListRows).ToList();
        }

        return rows;
    }

    public static IReadOnlyList<CategoryRuleRow>? ReadRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<CategoryRuleRow>();
        if (!TryArray(json, out var items))
            return null;

        var rows = new List<CategoryRuleRow>();
        foreach (var item in items)
        {
            if (!long.TryParse(Text(item, "id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
                continue;
            rows.Add(new CategoryRuleRow
            {
                Id = id,
                Timestamp = When(item, "timestamp"),
                Summary = RuleSummary(item),
                Inverted = item.TryGetProperty("matcher", out var matcher) && Flag(matcher, "inverted")
            });
        }

        return rows;
    }

    public static string? ReadCreatedId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            var id = Text(document.RootElement, "id");
            if (id.Length is < 1 or > 64)
                return null;
            return Guid.TryParse(id, out _) || PackageNamePattern.IsMatch(id) ? id : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Limit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= MaxText ? trimmed : trimmed[..MaxText];
    }

    private static (string? Start, string? End, string? Error) CheckRange(string? start, string? end, string label)
    {
        var startValue = ParseDate(start, label + " start", out var startError);
        var endValue = ParseDate(end, label + " end", out var endError);
        if (startError is not null)
            return (null, null, startError);
        if (endError is not null)
            return (null, null, endError);
        if (startValue is not null && endValue is not null && string.CompareOrdinal(endValue, startValue) < 0)
            return (null, null, label + " end is before " + label.ToLowerInvariant() + " start.");
        return (startValue, endValue, null);
    }

    private static string? ParseDate(string? value, string label, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            error = label + " must be a date (yyyy-MM-dd).";
            return null;
        }

        return value.Trim();
    }

    private static bool TryArray(string? json, out List<JsonElement> items)
    {
        items = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            items = document.RootElement.EnumerateArray().Select(item => item.Clone()).ToList();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return string.Empty;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            _ => string.Empty
        };
    }

    private static string When(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return string.Empty;
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
            return text ?? string.Empty;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var millis))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
            }
            catch (ArgumentOutOfRangeException)
            {
                return value.ToString();
            }
        }

        return string.Empty;
    }

    private static string? CheckMatcher(JsonElement matcher, int depth)
    {
        if (matcher.ValueKind != JsonValueKind.Object)
            return "A matcher must be an object.";
        if (depth > MaxMatcherDepth)
            return "A matcher is nested more than 8 levels.";

        var hasChildren = matcher.TryGetProperty("children", out var children);
        var hasField = matcher.TryGetProperty("field", out _);
        var hasRegex = matcher.TryGetProperty("regex", out _);
        if (hasChildren && (hasField || hasRegex))
            return "A matcher is either a regex or a composite, not both.";
        if (matcher.TryGetProperty("inverted", out var inverted)
            && inverted.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            return "inverted must be true or false.";

        if (hasChildren)
        {
            if (children.ValueKind != JsonValueKind.Array || children.GetArrayLength() == 0)
                return "A composite matcher needs at least one child.";
            if (children.GetArrayLength() > 50)
                return "A composite matcher has more than 50 children.";
            if (matcher.TryGetProperty("requiresAllChildren", out var all)
                && all.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                return "requiresAllChildren must be true or false.";
            foreach (var child in children.EnumerateArray())
            {
                var error = CheckMatcher(child, depth + 1);
                if (error is not null)
                    return error;
            }

            return null;
        }

        if (!hasField || !hasRegex)
            return "A regex matcher needs field and regex.";
        var field = Text(matcher, "field");
        if (!ResultFields.Contains(field, StringComparer.Ordinal))
            return "Field must be SEVERITY, CODE, MESSAGE, or EXPRESSION.";
        var pattern = Text(matcher, "regex");
        if (pattern.Length is < 1 or > 500)
            return "Regex is required and must be 500 characters or fewer.";
        if (!Compiles(pattern))
            return "Regex is not a valid pattern.";
        return null;
    }

    private static bool Compiles(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string MapAddressError(string error, string singular, string plural)
    {
        if (error.Contains("at least one", StringComparison.Ordinal))
            return "Add at least one " + singular + ".";
        if (error.Contains("20", StringComparison.Ordinal))
            return "A notification can list at most 20 " + plural + ".";
        return "Each " + singular + " must be an email address.";
    }

    private static bool Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static string RuleSummary(JsonElement item)
    {
        if (!item.TryGetProperty("matcher", out var matcher) || matcher.ValueKind != JsonValueKind.Object)
            return "No matcher";
        if (matcher.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            return "Composite (" + children.GetArrayLength() + ")";
        var field = Text(matcher, "field");
        var regex = Text(matcher, "regex");
        if (regex.Length > 80)
            regex = regex[..80] + "…";
        if (field.Length == 0 && regex.Length == 0)
            return "Matcher";
        return (field + " " + regex).Trim();
    }
}
