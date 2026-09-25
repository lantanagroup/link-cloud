using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Reads the manual-upload import sheet - a single-sheet .xlsx workbook (sheet "FHIR Import",
// Epic_Import_Sheet.xlsx / Cerner_Import_Sheet.xlsx in StaticAssets/excel-sheets) via ClosedXML,
// then flattens it into a plain row/column-letter lookup so the rest of this file can keep
// locating content by what it says rather than where it sits.
//
// The sheet is a series of labeled sections. Most are "Field Key" / "Field Label" / "Value" /
// "Description" rows - read by matching column A against a known field key, regardless of which
// row it lands on, since Epic and Cerner insert/omit rows (Cerner's POI section has SFTP rows
// Epic's doesn't) and a fixed cell reference would silently read the wrong field for one vendor.
// A few sections are repeating-row tables (Location Identifiers, Location Types, Managing
// Organizations, HSLOC Mapping, Encounter Mapping), located by their section-header text and read
// until the row sequence breaks (a section is followed by a blank, unserialized row in the xlsx,
// so a gap in row numbers reliably marks the table's end).
public sealed class ManualUploadTemplateService : IManualUploadTemplateService
{
    private readonly IOnboardingWriteService _writeService;
    private readonly IReferenceDataService _referenceDataService;

    public ManualUploadTemplateService(
        IOnboardingWriteService writeService,
        IReferenceDataService referenceDataService)
    {
        _writeService = writeService;
        _referenceDataService = referenceDataService;
    }

    private const string SheetName = "FHIR Import";
    private const string NoneAddedYet = "(none added yet)";

    // Keyed by the raw label stripped to bare lowercase letters, so any spacing/hyphenation/casing
    // of either the internal kebab-case key or the online step's own tab label (LocationOrgStep.tsx's
    // METHOD_LABEL_KEYS / onboarding.json's locationOrg.methods.*) resolves to the same method. Needs
    // its own explicit "managingorganization" entry - unlike the other three methods, the UI label
    // ("Managing Organization") and the internal key ("managing-org") don't collapse to the same
    // letters-only string, so naive stripping alone would silently miss it and land on null (which
    // used to hide the Managing Organization fields entirely, even though the internal key round-trips
    // fine).
    private static readonly Dictionary<string, string> LocationMethodAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["managingorg"] = "managing-org",
        ["managingorganization"] = "managing-org",
        ["managingorganizations"] = "managing-org",
        ["locationidentifier"] = "location-identifier",
        ["locationidentifiers"] = "location-identifier",
        ["locationtype"] = "location-type",
        ["locationtypes"] = "location-type",
        ["customfhirpath"] = "custom-fhir-path"
    };

    private static string? NormalizeLocationMethod(string rawMethod)
    {
        var normalized = Regex.Replace(rawMethod.Trim(), @"[^A-Za-z]", "").ToLowerInvariant();
        return LocationMethodAliases.GetValueOrDefault(normalized);
    }

    // Every repeating-table section header in the sheet. A facility that adds rows to one table by
    // inserting them in Excel pushes everything after it down, keeping the natural blank-row gap
    // that normally ends a table - but a row sequence that is NOT properly gapped (e.g. rows typed
    // in without an insert) must still not run into the next section, so ReadTableRows also stops
    // the moment it sees any of these, independent of the row-number gap.
    private static readonly HashSet<string> SectionHeaders = new(StringComparer.Ordinal)
    {
        "Organization Identification — Location Identifiers",
        "Organization Identification — Location Types",
        "Organization Identification — Managing Organizations",
        "HSLOC Location Mapping",
        "Encounter Mapping"
    };

    public async Task<ImportResult> ImportAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        Dictionary<int, Dictionary<string, string>> rows;
        try
        {
            rows = ReadRows(fileStream);
        }
        catch (InvalidDataException)
        {
            return InvalidFormatResult();
        }

        var errors = new List<ImportCellError>();
        var values = ReadScalarFields(rows, out var rowNumberByKey, errors);
        ValidateCrossFields(values, rowNumberByKey, rows, errors);

        // Hsloc/Encounter validate as they build (a partial or unresolved row needs the same raw
        // table data the builder already reads), appending straight into `errors` rather than
        // returning a separate list - same single `errors` collection every other validation in
        // this file uses.
        var fields = new ImportedFields
        {
            Fhir = BuildFhir(values),
            Census = BuildCensus(values),
            LocationOrg = BuildLocationOrg(values, rows),
            Hsloc = await BuildHslocAsync(rows, errors, cancellationToken),
            Encounter = await BuildEncounterAsync(rows, errors, cancellationToken)
        };

        var hslocImported = fields.Hsloc?.Mappings is {Count: > 0};
        var encounterImported = fields.Encounter?.Mappings is {Count: > 0};
        // Location Types, Location Identifiers and Managing Organizations are three separate
        // repeating-row tables backing one method each, but they're one section (Organization
        // Identification) and get one slot here, same as HSLOC and Encounter each get their own -
        // any one of the three having a row is enough to count the section as touched. locOrgMethod
        // and customFhirPath are scalar fields and already counted separately via ScalarFieldKeys;
        // this is only for the table data those two don't cover.
        var locationOrgTablesImported = fields.LocationOrg is { } locationOrg &&
            (locationOrg.LocationTypes is {Count: > 0} ||
             locationOrg.LocationIdentifiers is {Count: > 0} ||
             locationOrg.ManagingOrganizationIds is {Count: > 0});

        var totalFields = rows.Values.Count(row => row.TryGetValue("A", out var key) && ScalarFieldKeys.Contains(key))
            + 1 // Organization Identification (Location Types / Location Identifiers / Managing Organizations)
            + 1 // HSLOC Location Mapping
            + 1; // Encounter Mapping

        // Only a fully valid sheet gets saved - a cell error rejects the whole import (Accepted =
        // false), so nothing here is ever written half-validated. Accepted is captured only once
        // every validation - scalar, cross-field, and the Hsloc/Encounter builders' own row checks
        // above - has had a chance to add to `errors`, and stays true afterwards regardless of what
        // the save reports: it means "the sheet itself was well-formed," not "everything on it ended
        // up saved." A section can still fail once a real cross-service precondition or downstream
        // validator - never checkable from the sheet alone - rejects it; that's reported as a cell
        // error too, just added after the fact, so the facility sees why a field came back blank
        // instead of guessing, without losing the sections that DID save.
        var accepted = errors.Count == 0;
        if (accepted)
        {
            var saveResult = await _writeService.SaveImportedFieldsAsync(fields, cancellationToken);
            fields = BuildSavedFields(fields, saveResult);
            errors.AddRange(saveResult.SectionErrors.Select(sectionError => new ImportCellError
            {
                Sheet = SheetName,
                Cell = SectionHeaderCell(sectionError.Section),
                MessageKey = "onboarding:manualUpload.errors.saveFailed",
                Section = sectionError.Section,
                Detail = sectionError.Detail
            }));
        }

        return new ImportResult
        {
            Accepted = accepted,
            CellErrors = errors,
            FieldsImported = values.Count + (locationOrgTablesImported ? 1 : 0) + (hslocImported ? 1 : 0) + (encounterImported ? 1 : 0),
            TotalFields = totalFields,
            Fields = fields
        };
    }

    // No single cell to point at - a save failure belongs to a whole section, not one field - so
    // this names the section's own header text instead, which is at least somewhere real in the
    // sheet rather than a fabricated cell reference.
    private static string SectionHeaderCell(string section) => section switch
    {
        "fhir" => "FHIR Server Information",
        "census" => "Patients of Interest (POI) Configuration",
        "location-org" => "Organization Identification",
        "hsloc" => "HSLOC Location Mapping",
        "encounter" => "Encounter Mapping",
        _ => section
    };

    // Rebuilds the response from what the owning services actually saved (read back by
    // SaveImportedFieldsAsync), not from what was parsed - the same "read back rather than echo"
    // principle OnboardingWriteService.SaveAsync uses, so a value Link normalizes shows up
    // immediately and the frontend patches its draft from the true saved state. Only rebuilds a
    // section the sheet actually carried; a section the sheet said nothing about stays null rather
    // than picking up whatever the facility already had configured. sftpUsername/sftpPassword are
    // deliberately absent here - SaveImportedFieldsAsync already forwarded them to
    // SaveSftpCredentialsAsync, and a secret that's already been saved has no reason to round-trip
    // back to the browser.
    private static ImportedFields BuildSavedFields(ImportedFields original, ImportSaveResult saveResult)
    {
        var saved = saveResult.Draft;
        return new ImportedFields
        {
            Fhir = original.Fhir is null ? null : new ImportedFhir
            {
                FhirServerBaseUrl = saved.Fhir.FhirServerBaseUrl,
                MaxConcurrentRequests = saved.Fhir.MaxConcurrentRequests,
                MaxRetries = saved.Fhir.MaxRetries,
                MinAcquisitionPullTime = saved.Fhir.MinAcquisitionPullTime,
                MaxAcquisitionPullTime = saved.Fhir.MaxAcquisitionPullTime,
                LagDuration = saved.Fhir.LagDuration
            },
            Census = original.Census is null ? null : new ImportedCensus
            {
                PatientListIds = saved.Census.PatientListIds.Count > 0 ? new Dictionary<string, string>(saved.Census.PatientListIds) : null,
                SftpHost = saved.Census.SftpHost,
                SftpPort = saved.Census.SftpPort,
                SftpRemoteDirectory = saved.Census.SftpRemoteDirectory,
                SftpRemoveAfterProcessing = saved.Census.SftpRemoveAfterProcessing,
                AcquisitionFrequency = saved.Census.AcquisitionFrequency,
                HasCredentials = saveResult.SftpCredentialsSaved ? true : null
            },
            LocationOrg = original.LocationOrg is null ? null : new ImportedLocationOrg
            {
                Method = saved.LocationOrg.Method,
                CustomFhirPath = saved.LocationOrg.CustomFhirPath,
                ManagingOrganizationIds = saved.LocationOrg.ManagingOrganizationIds.Count > 0
                    ? saved.LocationOrg.ManagingOrganizationIds.ToList()
                    : null,
                LocationTypes = MergeLocationTypes(original.LocationOrg.LocationTypes, saved.LocationOrg.LocationTypes),
                LocationIdentifiers = MergeLocationIdentifiers(original.LocationOrg.LocationIdentifiers, saved.LocationOrg.LocationIdentifiers)
            },
            Hsloc = original.Hsloc is null ? null : new ImportedHsloc
            {
                Mappings = MergeHslocMappings(original.Hsloc.Mappings, saved.Hsloc.Mappings)
            },
            Encounter = original.Encounter is null ? null : new ImportedEncounter
            {
                Mappings = MergeEncounterMappings(original.Encounter.Mappings, saved.Encounter.Mappings)
            }
        };
    }

    // LocationOrgFhirPathBuilder.Build only turns a row into a saved condition once BOTH its
    // fields are filled in (a location-type row needs a Code AND an Alias to become a FHIRPath
    // clause at all) - a row the facility only half-filled in the sheet (e.g. a Code with no
    // Alias yet) never reaches Data Acquisition and so never comes back from the read-back save.
    // Restricted to rows `original` (this import) actually named: `saved` reflects the owning
    // service's FULL current state, which can include rows from a previous, unrelated
    // save/import - echoing all of it back here would resurrect those next to this sheet's own
    // rows even though this sheet never mentioned them. Each of this sheet's own rows uses the
    // saved (normalized) version when one round-tripped, and its own parsed version otherwise -
    // keyed on Code since that's the field a facility actually types.
    private static List<ImportedLocationType>? MergeLocationTypes(
        List<ImportedLocationType>? original, IReadOnlyList<LocationTypeEntry> saved)
    {
        if (original is null || original.Count == 0)
        {
            return null;
        }

        // GroupBy+First rather than ToDictionary: `saved` is another service's live state, not
        // data this method controls the shape of, so a duplicate key there must not crash the
        // whole import response.
        var savedByCode = saved.GroupBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var merged = original
            .Select(row => savedByCode.TryGetValue(row.Code, out var savedRow)
                ? new ImportedLocationType { Code = savedRow.Code, Alias = savedRow.Alias }
                : row)
            .ToList();
        return merged.Count > 0 ? merged : null;
    }

    // Same reasoning as MergeLocationTypes, keyed on System instead of Code.
    private static List<ImportedLocationIdentifier>? MergeLocationIdentifiers(
        List<ImportedLocationIdentifier>? original, IReadOnlyList<LocationIdentifierEntry> saved)
    {
        if (original is null || original.Count == 0)
        {
            return null;
        }

        var savedBySystem = saved.GroupBy(i => i.System, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var merged = original
            .Select(row => savedBySystem.TryGetValue(row.System, out var savedRow)
                ? new ImportedLocationIdentifier { System = savedRow.System, Code = savedRow.Code }
                : row)
            .ToList();
        return merged.Count > 0 ? merged : null;
    }

    // Normalization has no way to persist a mapping with a blank HslocCode (it's a foreign key to
    // the reference vocabulary, see HslocMappingService), so a row BuildHslocAsync kept for manual
    // selection - source code entered, reference code blank because it didn't resolve - never comes
    // back from the read-back save on its own. Restricted to rows `original` (this import) actually
    // named, same reasoning as MergeLocationTypes: `saved` is the facility's FULL current HSLOC
    // state, and a mapping this sheet never mentioned must never reappear just because it happens
    // to already exist. Each of this sheet's own source codes uses the saved (resolved) version
    // when it round-tripped, and its own parsed (still-blank) version otherwise.
    private static List<ImportedHslocMapping>? MergeHslocMappings(
        List<ImportedHslocMapping>? original, IReadOnlyList<Application.Models.Hsloc.HslocMapping> saved)
    {
        if (original is null || original.Count == 0)
        {
            return null;
        }

        var savedByCode = saved.GroupBy(m => m.SourceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        // Only a row THIS import actually resolved a code for prefers the read-back (Link-
        // normalized) value - a row it left blank (mismatch/unresolved) stays blank here even if
        // `saved` still has an older mapping for that source code. SaveImportedFieldsAsync protects
        // that older mapping from being deleted (see its own comment), but the facility still needs
        // to see "you haven't picked one for this import" rather than a stale value silently
        // reappearing as if it had resolved.
        var merged = original
            .Select(row => row.HslocCode.Length > 0 && savedByCode.TryGetValue(row.SourceCode, out var savedRow)
                ? new ImportedHslocMapping { SourceCode = savedRow.SourceCode, SourceDisplay = savedRow.SourceDisplay, HslocCode = savedRow.HslocCode }
                : row)
            .ToList();
        return merged.Count > 0 ? merged : null;
    }

    // Same reasoning as MergeHslocMappings: a row with a blank EncounterType (local system/code
    // entered, reference columns didn't resolve) never round-trips through Normalization's Code Map
    // operation on its own - EncounterMappingService.BuildCodeSystemMaps skips anything it can't
    // split on '|'. Restricted to ro   ws `original` (this import) actually named: `saved` is the
    // facility's FULL current Code Map operation, which can hold mappings from a previous, unrelated
    // save - echoing all of it back would resurrect those next to this sheet's own rows. Keyed by
    // (System, Code) since that pair, not a single source code, is what identifies one row.
    private static List<ImportedEncounterMapping>? MergeEncounterMappings(
        List<ImportedEncounterMapping>? original, IReadOnlyList<Application.Models.Encounter.EncounterMapping> saved)
    {
        if (original is null || original.Count == 0)
        {
            return null;
        }

        var savedByKey = saved.GroupBy(m => (m.System.ToUpperInvariant(), m.Code.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.First());
        // Same reasoning as MergeHslocMappings: only a row THIS import actually resolved a
        // reference for prefers the read-back value - a row it left blank (mismatch/unresolved)
        // stays blank even if `saved` still has an older mapping for that local system/code.
        var merged = original
            .Select(row => row.EncounterType.Length > 0 && savedByKey.TryGetValue((row.System.ToUpperInvariant(), row.Code.ToUpperInvariant()), out var savedRow)
                ? new ImportedEncounterMapping { System = savedRow.System, Code = savedRow.Code, Display = savedRow.Display, EncounterType = savedRow.EncounterType }
                : row)
            .ToList();
        return merged.Count > 0 ? merged : null;
    }

    private static ImportResult InvalidFormatResult() => new()
    {
        Accepted = false,
        CellErrors =
        [
            new ImportCellError
            {
                Sheet = SheetName,
                Cell = "A1",
                MessageKey = "onboarding:manualUpload.errors.invalidFormat"
            }
        ],
        FieldsImported = 0,
        TotalFields = 0
    };

    // ---------------------------------------------------------------- scalar "Field Key" rows

    // Field Key column values recognized anywhere in the sheet, keyed to the section/property they
    // belong to. usesAdt is intentionally absent - there is no draft field for it yet.
    private static readonly HashSet<string> ScalarFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "fhirBaseUrl", "maxConcurrentRequests", "maxRetries", "minPullTime", "maxPullTime",
        "patientLagDays", "patientLagHours", "patientLagMinutes",
        "censusFreqHours", "censusFreqMinutes",
        "admitLt24", "admit24to48", "admitGt48", "dischargeLt24", "discharge24to48", "dischargeGt48",
        "sftpHost", "sftpPort", "sftpUsername", "sftpPassword", "remoteDirectory", "removeFilesAfterProcessing",
        "locOrgMethod", "customFhirPath"
    };

    private static readonly Dictionary<string, string> CensusListKeyByFieldKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admitLt24"] = "admit-lt-24",
        ["admit24to48"] = "admit-24-to-48",
        ["admitGt48"] = "admit-gt-48",
        ["dischargeLt24"] = "discharge-lt-24",
        ["discharge24to48"] = "discharge-24-to-48",
        ["dischargeGt48"] = "discharge-gt-48"
    };

    private static readonly HashSet<string> FhirIdKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "admitLt24", "admit24to48", "admitGt48", "dischargeLt24", "discharge24to48", "dischargeGt48"
    };

    private static readonly HashSet<string> PullTimeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "minPullTime", "maxPullTime"
    };

    // Which onboarding step (StepId in types.ts) each scalar field belongs to, so a cell error can
    // tell the frontend which step in the nav to flag. Table-row sections (location-org, hsloc,
    // encounter) never produce a cell error and need no entry here - hsloc/encounter rows that fail
    // reference-table or uniqueness checks are silently dropped instead (see BuildHslocAsync/
    // BuildEncounterAsync), and location-org has no such checks at all.
    private static readonly Dictionary<string, string> SectionByFieldKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fhirBaseUrl"] = "fhir",
        ["maxConcurrentRequests"] = "fhir",
        ["maxRetries"] = "fhir",
        ["minPullTime"] = "fhir",
        ["maxPullTime"] = "fhir",
        ["patientLagDays"] = "fhir",
        ["patientLagHours"] = "fhir",
        ["patientLagMinutes"] = "fhir",
        ["censusFreqHours"] = "census",
        ["censusFreqMinutes"] = "census",
        ["admitLt24"] = "census",
        ["admit24to48"] = "census",
        ["admitGt48"] = "census",
        ["dischargeLt24"] = "census",
        ["discharge24to48"] = "census",
        ["dischargeGt48"] = "census",
        ["sftpHost"] = "census",
        ["sftpPort"] = "census",
        ["sftpUsername"] = "census",
        ["sftpPassword"] = "census",
        ["remoteDirectory"] = "census",
        ["removeFilesAfterProcessing"] = "census",
        ["locOrgMethod"] = "location-org",
        ["customFhirPath"] = "location-org"
    };

    private static string? SectionFor(string key) => SectionByFieldKey.GetValueOrDefault(key);

    private static Dictionary<string, string> ReadScalarFields(
        Dictionary<int, Dictionary<string, string>> rows,
        out Dictionary<string, int> rowNumberByKey,
        List<ImportCellError> errors)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        rowNumberByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rowNumber, row) in rows.OrderBy(kv => kv.Key))
        {
            if (!row.TryGetValue("A", out var key) || !ScalarFieldKeys.Contains(key))
            {
                continue;
            }

            rowNumberByKey[key] = rowNumber;

            // Nothing here is required - a blank cell is just skipped. Only a value that IS
            // present gets checked, and only for whether it's well-formed (see ValidateScalar).
            var raw = row.GetValueOrDefault("C");
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var value = raw.Trim();
            if (PullTimeKeys.Contains(key))
            {
                value = NormalizeExcelTime(value);
            }

            var (messageKey, detail) = ValidateScalar(key, value);
            if (messageKey is not null)
            {
                // Recorded as an error (which rejects the whole import - Accepted=false - so
                // Fields is never consumed by the client) but still counted as "had a value" and
                // still parsed where it harmlessly fails to (ParseInt simply returns null for it).
                // Never includes the raw value in the error - sftpUsername/sftpPassword in
                // particular must never be echoed back, even indirectly.
                errors.Add(new ImportCellError
                {
                    Sheet = SheetName,
                    Cell = $"C{rowNumber}",
                    MessageKey = messageKey,
                    Section = SectionFor(key),
                    Label = row.GetValueOrDefault("B"),
                    Detail = detail
                });
            }

            values[key] = value;
        }

        return values;
    }

    private static (string? MessageKey, string? Detail) ValidateScalar(string key, string value)
    {
        if (FhirIdKeys.Contains(key))
        {
            return (ValidateFhirId(value), null);
        }

        return key switch
        {
            "fhirBaseUrl" => (ValidateAbsoluteUrl(value), null),
            "maxConcurrentRequests" => ValidateIntRange(value, FieldValidationRules.MaxConcurrentRequestsMin, FieldValidationRules.MaxConcurrentRequestsCap),
            "maxRetries" => ValidateIntRange(value, FieldValidationRules.MaxRetriesMin, FieldValidationRules.MaxRetriesCap),
            "minPullTime" or "maxPullTime" => (ValidatePullTime(value), null),
            "sftpHost" => (ValidateSftpHost(value), null),
            "sftpPort" => ValidateIntRange(value, FieldValidationRules.SftpPortMin, FieldValidationRules.SftpPortMax),
            "censusFreqHours" or "censusFreqMinutes"
                or "patientLagDays" or "patientLagHours" or "patientLagMinutes" => (ValidateNonNegativeInteger(value), null),
            "locOrgMethod" => (ValidateLocationMethod(value), null),
            "customFhirPath" => (ValidateFhirPath(value), null),
            _ => (null, null)
        };
    }

    private static string? ValidateAbsoluteUrl(string value) =>
        FieldValidationRules.IsAbsoluteUrl(value) ? null : "onboarding:manualUpload.errors.invalidUrl";

    private static string? ValidateSftpHost(string value) =>
        FieldValidationRules.IsValidSftpHost(value) ? null : "onboarding:manualUpload.errors.invalidSftpHost";

    // Reuses the same alias table BuildLocationOrg normalizes with, rather than a separate list of
    // "valid" strings - a value this rejects would otherwise silently normalize to null and vanish
    // from the saved draft with no error anywhere, which is exactly the gap that let a typo'd
    // method go unnoticed before this check existed.
    private static string? ValidateLocationMethod(string value) =>
        NormalizeLocationMethod(value) is not null ? null : "onboarding:manualUpload.errors.invalidLocationMethod";

    private static string? ValidateFhirPath(string value) =>
        FieldValidationRules.IsPlausibleFhirPath(value) ? null : "onboarding:manualUpload.errors.invalidFhirPath";

    private static string? ValidateNonNegativeInteger(string value) =>
        FieldValidationRules.IsNonNegativeInteger(value) ? null : "onboarding:manualUpload.errors.invalidNumber";

    // Detail carries the actual min/max so the facility sees the real allowed range instead of a
    // generic "within the allowed range" - interpolated into the message via {{detail}}, same
    // mechanism SaveFailed already uses for a downstream service's own explanation.
    private static (string? MessageKey, string? Detail) ValidateIntRange(string value, int min, int max) =>
        FieldValidationRules.IsIntInRange(value, min, max)
            ? (null, null)
            : ("onboarding:manualUpload.errors.invalidNumberRange", $"{min}-{max}");

    private static string? ValidatePullTime(string value) =>
        FieldValidationRules.IsPullTime(value) ? null : "onboarding:manualUpload.errors.invalidPullTime";

    // Excel stores a time-formatted cell as a fraction of a 24-hour day (13:11 -> 0.549305...), not
    // as "13:11" text - our reader has no access to the cell's number format (that lives in
    // xl/styles.xml, which ReadRows never parses), so a time cell round-trips here as a bare
    // decimal. minPullTime/maxPullTime are the only fields where a facility is expected to type an
    // actual time of day, so this only ever fires for those two - any other numeric field's value
    // is left untouched and falls through to ValidateScalar/ValidateNonNegativeInteger as normal.
    private static string NormalizeExcelTime(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) ||
            fraction < 0 || fraction >= 1)
        {
            return value;
        }

        var totalMinutes = (int)Math.Round(fraction * 24 * 60, MidpointRounding.AwayFromZero) % (24 * 60);
        return $"{totalMinutes / 60:D2}:{totalMinutes % 60:D2}";
    }

    private static string? ValidateFhirId(string value) =>
        FieldValidationRules.IsFhirId(value) ? null : "onboarding:manualUpload.errors.invalidFhirId";

    // Rules that span more than one field, so they can't be checked cell-by-cell in ReadScalarFields.
    // Nothing here fires just because a field is blank - only when the values actually present
    // don't add up (e.g. a lag duration that's too long).
    private static void ValidateCrossFields(
        Dictionary<string, string> values,
        Dictionary<string, int> rowNumberByKey,
        Dictionary<int, Dictionary<string, string>> rows,
        List<ImportCellError> errors)
    {
        var lagDays = ParseInt(values, "patientLagDays");
        var lagHours = ParseInt(values, "patientLagHours");
        var lagMinutes = ParseInt(values, "patientLagMinutes");
        var totalLagMinutes = FieldValidationRules.LagTotalMinutes(lagDays ?? 0, lagHours ?? 0, lagMinutes ?? 0);
        if (totalLagMinutes > FieldValidationRules.LagDurationCapMinutes && rowNumberByKey.TryGetValue("patientLagDays", out var lagRow))
        {
            errors.Add(new ImportCellError
            {
                Sheet = SheetName,
                Cell = $"C{lagRow}",
                MessageKey = "onboarding:manualUpload.errors.lagDurationTooLong",
                Section = "fhir",
                Label = LabelForRow(rows, lagRow)
            });
        }
        // Required, not merely "must total > 0 once touched": leaving all three blank is exactly as
        // much an error as filling them in with zeros - a facility must actually set a lag, not skip
        // the group entirely.
        else if (totalLagMinutes <= 0 && rowNumberByKey.TryGetValue("patientLagDays", out var lagZeroRow))
        {
            errors.Add(new ImportCellError
            {
                Sheet = SheetName,
                Cell = $"C{lagZeroRow}",
                MessageKey = "onboarding:manualUpload.errors.requiredLagDuration",
                Section = "fhir",
                Label = LabelForRow(rows, lagZeroRow)
            });
        }

        var censusHours = ParseInt(values, "censusFreqHours");
        var censusMinutes = ParseInt(values, "censusFreqMinutes");
        // Required, not merely "must be valid once either is touched" - Hours and Minutes are an
        // either/or pair (the sheet's own "Required" column: "Yes - if Minutes/Hours not provided"),
        // so leaving both blank is the same failure as an out-of-range total: no acquisition
        // frequency at all.
        if (!FieldValidationRules.IsValidCensusFrequency(censusHours ?? 0, censusMinutes ?? 0))
        {
            var censusRow = rowNumberByKey.TryGetValue("censusFreqHours", out var hoursRow)
                ? hoursRow
                : rowNumberByKey.GetValueOrDefault("censusFreqMinutes");
            if (censusRow > 0)
            {
                errors.Add(new ImportCellError
                {
                    Sheet = SheetName,
                    Cell = $"C{censusRow}",
                    MessageKey = "onboarding:manualUpload.errors.invalidCensusFrequency",
                    Section = "census",
                    Label = LabelForRow(rows, censusRow)
                });
            }
        }

        // WriteFhirSectionAsync (OnboardingWriteService) won't write ANY of the FHIR section unless
        // both of these are present - a sheet that fills in only some of the section used to pass
        // validation, get "Accepted", and then have the whole section silently dropped at save time
        // with no error anywhere, so they're always required here.
        RequireFields(
            values, rowNumberByKey, rows, errors, "fhir",
            "onboarding:manualUpload.errors.requiredFhirBundle",
            "fhirBaseUrl", "maxConcurrentRequests");

        // The pull times are optional, but only as a pair: both blank means no pull-time window,
        // while only one set is rejected by UpdateFhirServerInfoAsync - so the missing side is
        // flagged here instead of failing the whole section at save time.
        if (values.ContainsKey("minPullTime") != values.ContainsKey("maxPullTime"))
        {
            RequireFields(
                values, rowNumberByKey, rows, errors, "fhir",
                "onboarding:manualUpload.errors.requiredPullTimePair",
                "minPullTime", "maxPullTime");
        }

        // Ordering only means something once both are present and individually well-formed - a
        // malformed pull time already has its own invalidPullTime cell error from ValidateScalar,
        // and reporting an ordering problem on top of that would just be noise about a value that's
        // already flagged as wrong.
        if (values.TryGetValue("minPullTime", out var minPullTime) && values.TryGetValue("maxPullTime", out var maxPullTime) &&
            FieldValidationRules.IsPullTime(minPullTime) && FieldValidationRules.IsPullTime(maxPullTime) &&
            !FieldValidationRules.IsPullTimeOrderValid(minPullTime, maxPullTime) &&
            rowNumberByKey.TryGetValue("minPullTime", out var minPullTimeRow))
        {
            errors.Add(new ImportCellError
            {
                Sheet = SheetName,
                Cell = $"C{minPullTimeRow}",
                MessageKey = "onboarding:manualUpload.errors.invalidPullTimeOrder",
                Section = "fhir",
                Label = LabelForRow(rows, minPullTimeRow)
            });
        }

        // Epic's template is the only one with FHIR List admit/discharge id rows at all (see
        // ScalarFieldKeys' own comment on Epic/Cerner row differences) - their presence in
        // rowNumberByKey is how this tells an Epic sheet from a Cerner one, without needing a
        // separate "Vendor:" line parse. All six are one FHIR List configuration in Data
        // Acquisition; a facility that fills in some but not all would otherwise get a census
        // silently missing whichever ids they skipped.
        var epicCensusListKeys = CensusListKeyByFieldKey.Keys.ToArray();
        if (epicCensusListKeys.Any(rowNumberByKey.ContainsKey))
        {
            RequireFields(
                values, rowNumberByKey, rows, errors, "census",
                "onboarding:manualUpload.errors.requiredFhirListId",
                epicCensusListKeys);
        }

        // Cerner's template is the only one with SFTP rows at all - same row-presence detection as
        // the Epic block above. Unlike the FHIR bundle (optional until touched), a Cerner sheet's
        // POI acquisition method IS SFTP, so these four are simply required, not "required once one
        // of them is filled in."
        var cernerSftpKeys = new[] {"sftpHost", "sftpPort", "sftpUsername", "sftpPassword"};
        if (cernerSftpKeys.Any(rowNumberByKey.ContainsKey))
        {
            RequireFields(
                values, rowNumberByKey, rows, errors, "census",
                "onboarding:manualUpload.errors.requiredSftpField",
                cernerSftpKeys);
        }

        // No requiredness here (a facility can leave Resolution Method blank, or leave both tables
        // untouched, and still move on - see BuildLocationOrg) - only "a row that has anything in it
        // has BOTH of its columns," independent of which method is selected. A row with just a Code
        // or just an Alias/System can't have meant to submit it, and LocationOrgFhirPathBuilder.Build
        // already can't turn a half-filled row into a FHIRPath clause anyway.
        FlagPartialRows(
            rows, errors, "Organization Identification — Location Types", "location-org", "A", "B",
            "onboarding:manualUpload.errors.partialLocationType");
        FlagPartialRows(
            rows, errors, "Organization Identification — Location Identifiers", "location-org", "A", "B",
            "onboarding:manualUpload.errors.partialLocationIdentifier");
    }

    // Flags every row in a two-column table that has SOME data but not both columns - a fully blank
    // row is left alone (nothing was attempted), and a fully complete row is left alone (nothing
    // wrong with it). No "at least one complete row" requirement here - see the two call sites above.
    private static void FlagPartialRows(
        Dictionary<int, Dictionary<string, string>> rows,
        List<ImportCellError> errors,
        string sectionHeader,
        string section,
        string columnA,
        string columnB,
        string partialMessageKey)
    {
        foreach (var (rowNumber, row) in ReadTableRows(rows, sectionHeader))
        {
            var a = row.GetValueOrDefault(columnA, "").Trim();
            var b = row.GetValueOrDefault(columnB, "").Trim();
            if (a.Length == 0 && b.Length == 0)
            {
                continue;
            }
            if (a.Length == 0 || b.Length == 0)
            {
                errors.Add(new ImportCellError
                {
                    Sheet = SheetName,
                    Cell = $"{(a.Length == 0 ? columnA : columnB)}{rowNumber}",
                    MessageKey = partialMessageKey,
                    Section = section
                });
            }
        }
    }

    // Every key in `keys` must have a value, independent of whether any of the others do - unlike
    // RequireTogether, this isn't "all or nothing starting from any one," it's "always."
    private static void RequireFields(
        Dictionary<string, string> values,
        Dictionary<string, int> rowNumberByKey,
        Dictionary<int, Dictionary<string, string>> rows,
        List<ImportCellError> errors,
        string section,
        string messageKey,
        params string[] keys)
    {
        foreach (var missingKey in keys.Where(key => !values.ContainsKey(key)))
        {
            if (!rowNumberByKey.TryGetValue(missingKey, out var missingRow))
            {
                continue;
            }

            errors.Add(new ImportCellError
            {
                Sheet = SheetName,
                Cell = $"C{missingRow}",
                MessageKey = messageKey,
                Section = section,
                Label = LabelForRow(rows, missingRow)
            });
        }
    }


    private static string? LabelForRow(Dictionary<int, Dictionary<string, string>> rows, int rowNumber) =>
        rows.TryGetValue(rowNumber, out var row) ? row.GetValueOrDefault("B") : null;

    private static int? ParseInt(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    // A TRUE/FALSE cell a facility ticks as an actual Excel checkbox/boolean (rather than typing the
    // word) serializes in the underlying XML as "1"/"0", not the text "True"/"False" - ReadCellText
    // passes that raw numeric string straight through, and bool.TryParse only recognizes the words,
    // so a genuinely-true boolean cell silently read back as null (then defaulted to false) with no
    // error anywhere. "1"/"0" need their own check, not just TryParse's word-based one.
    private static bool? ParseBool(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var raw))
        {
            return null;
        }
        if (bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }
        return raw.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => null
        };
    }

    // ---------------------------------------------------------------- section builders

    private static ImportedFhir? BuildFhir(Dictionary<string, string> values)
    {
        var lagDuration = BuildDuration(
            values, "patientLagDays", "patientLagHours", "patientLagMinutes",
            (days, hours, minutes) => $"P{days}DT{hours}H{minutes}M");

        var fhir = new ImportedFhir
        {
            FhirServerBaseUrl = values.GetValueOrDefault("fhirBaseUrl"),
            MaxConcurrentRequests = ParseInt(values, "maxConcurrentRequests"),
            MaxRetries = ParseInt(values, "maxRetries"),
            MinAcquisitionPullTime = values.GetValueOrDefault("minPullTime"),
            MaxAcquisitionPullTime = values.GetValueOrDefault("maxPullTime"),
            LagDuration = lagDuration
        };

        return IsEmpty(fhir) ? null : fhir;
    }

    private static ImportedCensus? BuildCensus(Dictionary<string, string> values)
    {
        var acquisitionFrequency = BuildDuration(
            values, "censusFreqHours", "censusFreqMinutes", null,
            (hours, minutes, _) => $"PT{hours}H{minutes}M");

        var patientListIds = CensusListKeyByFieldKey
            .Where(pair => values.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Value, pair => values[pair.Key]);

        var census = new ImportedCensus
        {
            PatientListIds = patientListIds.Count > 0 ? patientListIds : null,
            SftpHost = values.GetValueOrDefault("sftpHost"),
            SftpPort = ParseInt(values, "sftpPort"),
            SftpRemoteDirectory = values.GetValueOrDefault("remoteDirectory"),
            SftpRemoveAfterProcessing = ParseBool(values, "removeFilesAfterProcessing"),
            AcquisitionFrequency = acquisitionFrequency,
            SftpUsername = values.GetValueOrDefault("sftpUsername"),
            SftpPassword = values.GetValueOrDefault("sftpPassword")
        };

        return IsEmpty(census) ? null : census;
    }

    private static ImportedLocationOrg? BuildLocationOrg(
        Dictionary<string, string> values,
        Dictionary<int, Dictionary<string, string>> rows)
    {
        var method = values.TryGetValue("locOrgMethod", out var rawMethod) ? NormalizeLocationMethod(rawMethod) : null;

        var locationIdentifiers = ReadTableRows(rows, "Organization Identification — Location Identifiers")
            .Select(entry => new ImportedLocationIdentifier { System = entry.Row.GetValueOrDefault("A", ""), Code = entry.Row.GetValueOrDefault("B", "") })
            .Where(entry => entry.System.Length > 0 || entry.Code.Length > 0)
            .ToList();

        var locationTypes = ReadTableRows(rows, "Organization Identification — Location Types")
            .Select(entry => new ImportedLocationType { Code = entry.Row.GetValueOrDefault("A", ""), Alias = entry.Row.GetValueOrDefault("B", "") })
            .Where(entry => entry.Code.Length > 0 || entry.Alias.Length > 0)
            .ToList();

        var managingOrganizationIds = ReadTableRows(rows, "Organization Identification — Managing Organizations")
            .Select(entry => entry.Row.GetValueOrDefault("A", "").Trim())
            .Where(id => id.Length > 0)
            .ToList();

        var locationOrg = new ImportedLocationOrg
        {
            Method = method,
            CustomFhirPath = values.GetValueOrDefault("customFhirPath"),
            LocationIdentifiers = locationIdentifiers.Count > 0 ? locationIdentifiers : null,
            LocationTypes = locationTypes.Count > 0 ? locationTypes : null,
            ManagingOrganizationIds = managingOrganizationIds.Count > 0 ? managingOrganizationIds : null
        };

        return IsEmpty(locationOrg) ? null : locationOrg;
    }

    // At least one mapping is required (the sheet's own "Required" column: "Yes — at least one
    // complete mapping required"), and every row present must have its local half complete: a
    // source code, not just a display value. The reference half (HSLOC Code) is a different story -
    // it's only ever something a facility copied off the HSLOC reference table, so a code that's
    // blank or doesn't resolve there (typo'd, stale, or never filled in) is left blank rather than
    // raising a cell error. HslocCode comes back "" either way, which HslocStep's own mapping-row
    // <select> already renders as its "Select an HSLOC code" placeholder (the same state a
    // brand-new row starts in online) - the facility sees every location they entered and just has
    // to pick the right code for the ones that didn't resolve, instead of the whole import being
    // rejected over it. Also enforces "one Location.identifier.value maps to one HSLOC code": both
    // Epic's and Cerner's templates share this exact table shape, so a facility pasting a row twice
    // or reusing an identifier by mistake is a possible mistake on either vendor's sheet, not
    // vendor-specific. Every row sharing a duplicate identifier is flagged - not just the second
    // one onward - so the facility sees all of them and can tell which to fix, rather than a silent
    // "keep the first, drop the rest" that used to leave a genuinely different row (different local
    // code, different target) missing with no explanation at all.
    private async Task<ImportedHsloc?> BuildHslocAsync(
        Dictionary<int, Dictionary<string, string>> rows,
        List<ImportCellError> errors,
        CancellationToken cancellationToken)
    {
        const string sectionHeader = "HSLOC Location Mapping";
        var rawRows = ReadTableRows(rows, sectionHeader);
        if (rawRows.Count == 0)
        {
            RequireErrorAtHeader(rows, errors, sectionHeader, "hsloc", "onboarding:manualUpload.errors.requiredHslocMapping");
            return null;
        }

        var referenceCodes = await _referenceDataService.GetHslocCodesAsync(cancellationToken);
        var referenceByCode = referenceCodes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var duplicateSourceCodes = rawRows
            .Select(entry => entry.Row.GetValueOrDefault("B", "").Trim())
            .Where(code => code.Length > 0)
            .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mappings = new List<ImportedHslocMapping>();
        foreach (var (rowNumber, row) in rawRows)
        {
            var sourceCode = row.GetValueOrDefault("B", "").Trim();
            var hslocCode = row.GetValueOrDefault("C", "").Trim();
            var sourceDisplay = row.GetValueOrDefault("A", "").Trim();
            if (sourceDisplay.Length == 0 && sourceCode.Length == 0 && hslocCode.Length == 0)
            {
                continue;
            }
            if (sourceCode.Length == 0)
            {
                errors.Add(new ImportCellError {Sheet = SheetName, Cell = $"B{rowNumber}", MessageKey = "onboarding:manualUpload.errors.partialHslocMapping", Section = "hsloc"});
                continue;
            }
            if (duplicateSourceCodes.Contains(sourceCode))
            {
                errors.Add(new ImportCellError {Sheet = SheetName, Cell = $"B{rowNumber}", MessageKey = "onboarding:manualUpload.errors.duplicateHslocIdentifier", Section = "hsloc"});
                continue;
            }

            // Left blank on a mismatch/unresolved code rather than raising a cell error - this is
            // the honest "you still need to pick one" signal HslocStep's own <select> already
            // renders as its "Select an HSLOC code" placeholder. OnboardingWriteService.SaveImportedFieldsAsync
            // and MergeHslocMappings (below) are what keep this from also deleting an existing,
            // previously-resolved mapping for the same local code - this method only decides what
            // the facility SEES, not what stays protected in Normalization.
            var resolvedHslocCode = hslocCode.Length > 0 && referenceByCode.TryGetValue(hslocCode, out var reference)
                ? reference.Code
                : "";

            mappings.Add(new ImportedHslocMapping
            {
                SourceCode = sourceCode,
                SourceDisplay = sourceDisplay.Length > 0 ? sourceDisplay : null,
                HslocCode = resolvedHslocCode
            });
        }

        if (mappings.Count == 0)
        {
            RequireErrorAtHeader(rows, errors, sectionHeader, "hsloc", "onboarding:manualUpload.errors.requiredHslocMapping");
            return null;
        }

        return new ImportedHsloc { Mappings = mappings };
    }

    private static void RequireErrorAtHeader(
        Dictionary<int, Dictionary<string, string>> rows,
        List<ImportCellError> errors,
        string sectionHeader,
        string section,
        string messageKey)
    {
        var headerRow = FindSectionHeaderRow(rows, sectionHeader);
        if (headerRow is not null)
        {
            errors.Add(new ImportCellError {Sheet = SheetName, Cell = $"A{headerRow}", MessageKey = messageKey, Section = section});
        }
    }

    // Encounter Mapping as a whole stays optional - zero rows is fine, matching the sheet's own
    // "Required" column (no "at least one" note on this table, unlike HSLOC). A row that IS present
    // must have its local half complete: system AND code, not just one of them. The reference half
    // (Reference Code System/Code) is a different story - it's only ever something a facility
    // copied off the reference table, so a value that's blank or doesn't resolve there (typo'd,
    // stale, or never filled in) is left blank rather than raising a cell error. EncounterType comes
    // back "" either way, which EncounterStep's own target-code picker already renders as an empty
    // combobox ready for a manual pick (the same state a freshly added row starts in online) - the
    // facility sees every local code they entered and just has to pick the right target for the ones
    // that didn't resolve, instead of the whole import being rejected over it. The resolved
    // System/Code/Display come from the reference row rather than the sheet's own text so a
    // facility's casing/whitespace on the reference columns never diverges from what Link's
    // reference table holds.
    private async Task<ImportedEncounter?> BuildEncounterAsync(
        Dictionary<int, Dictionary<string, string>> rows,
        List<ImportCellError> errors,
        CancellationToken cancellationToken)
    {
        var rawRows = ReadTableRows(rows, "Encounter Mapping");
        if (rawRows.Count == 0)
        {
            return null;
        }

        var referenceCodes = await _referenceDataService.GetEncounterCodesAsync(cancellationToken);
        var referenceByKey = referenceCodes.ToDictionary(
            c => (System: c.System.ToUpperInvariant(), Code: c.Code.ToUpperInvariant()));

        var mappings = new List<ImportedEncounterMapping>();
        foreach (var (rowNumber, row) in rawRows)
        {
            var system = row.GetValueOrDefault("A", "").Trim();
            var code = row.GetValueOrDefault("B", "").Trim();
            var referenceSystem = row.GetValueOrDefault("C", "").Trim();
            var referenceCode = row.GetValueOrDefault("D", "").Trim();
            if (system.Length == 0 && code.Length == 0 && referenceSystem.Length == 0 && referenceCode.Length == 0)
            {
                continue;
            }
            if (system.Length == 0 || code.Length == 0)
            {
                errors.Add(new ImportCellError {Sheet = SheetName, Cell = $"{(system.Length == 0 ? "A" : "B")}{rowNumber}", MessageKey = "onboarding:manualUpload.errors.partialEncounterMapping", Section = "encounter"});
                continue;
            }

            // Left blank on a mismatch/unresolved reference rather than raising a cell error - this
            // is the honest "you still need to pick a target" signal EncounterStep's own picker
            // already renders as an empty combobox. OnboardingWriteService.SaveImportedFieldsAsync
            // and MergeEncounterMappings (below) are what keep this from also deleting an existing,
            // previously-resolved mapping for the same local system/code - this method only decides
            // what the facility SEES, not what stays protected in Normalization.
            var resolved = referenceSystem.Length > 0 && referenceCode.Length > 0 &&
                referenceByKey.TryGetValue((referenceSystem.ToUpperInvariant(), referenceCode.ToUpperInvariant()), out var reference)
                ? reference
                : null;

            mappings.Add(new ImportedEncounterMapping
            {
                System = system,
                Code = code,
                Display = resolved?.Display,
                EncounterType = resolved is null ? "" : $"{resolved.System}|{resolved.Code}"
            });
        }

        return mappings.Count > 0 ? new ImportedEncounter { Mappings = mappings } : null;
    }

    private static string? BuildDuration(
        Dictionary<string, string> values,
        string firstKey,
        string secondKey,
        string? thirdKey,
        Func<int, int, int, string> format)
    {
        var first = ParseInt(values, firstKey);
        var second = ParseInt(values, secondKey);
        var third = thirdKey is null ? null : ParseInt(values, thirdKey);
        if (first is null && second is null && third is null)
        {
            return null;
        }
        return format(first ?? 0, second ?? 0, third ?? 0);
    }

    private static bool IsEmpty(ImportedFhir fhir) =>
        fhir is { FhirServerBaseUrl: null, MaxConcurrentRequests: null, MaxRetries: null, MinAcquisitionPullTime: null, MaxAcquisitionPullTime: null, LagDuration: null };

    private static bool IsEmpty(ImportedCensus census) =>
        census is
        {
            PatientListIds: null, SftpHost: null, SftpPort: null, SftpRemoteDirectory: null,
            SftpRemoveAfterProcessing: null, AcquisitionFrequency: null, SftpUsername: null, SftpPassword: null
        };

    private static bool IsEmpty(ImportedLocationOrg locationOrg) =>
        locationOrg is
        {
            Method: null, CustomFhirPath: null, LocationIdentifiers: null, LocationTypes: null, ManagingOrganizationIds: null
        };

    // ---------------------------------------------------------------- repeating-row tables

    // Finds a section by its header text, skips the following column-label row, then reads every
    // row up to the next known section header (or the end of the sheet if this is the last
    // table). Deliberately does NOT stop at the first row number that doesn't exist: Excel never
    // serializes a genuinely blank row at all, and a facility leaving one *inside* their own data
    // - between two rows they filled in, not just trailing after the last one - is a gap this
    // must skip over rather than treat as the table's end, or everything after that gap silently
    // vanishes even though it's still inside the same table.
    private static int? FindSectionHeaderRow(Dictionary<int, Dictionary<string, string>> rows, string sectionHeader) =>
        rows
            .Where(kv => kv.Value.TryGetValue("A", out var text) && string.Equals(text, sectionHeader, StringComparison.Ordinal))
            .Select(kv => (int?)kv.Key)
            .FirstOrDefault();

    private static List<(int RowNumber, Dictionary<string, string> Row)> ReadTableRows(
        Dictionary<int, Dictionary<string, string>> rows,
        string sectionHeader)
    {
        var headerRow = FindSectionHeaderRow(rows, sectionHeader);

        if (headerRow is null)
        {
            return [];
        }

        var nextSectionRow = rows
            .Where(kv => kv.Key > headerRow.Value && kv.Value.TryGetValue("A", out var text) && SectionHeaders.Contains(text))
            .Select(kv => kv.Key)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        var lastDataRow = rows.Keys
            .Where(rowNumber => rowNumber > headerRow.Value && rowNumber < nextSectionRow)
            .DefaultIfEmpty(headerRow.Value)
            .Max();

        var collected = new List<(int RowNumber, Dictionary<string, string> Row)>();
        for (var rowNumber = headerRow.Value + 2; rowNumber <= lastDataRow; rowNumber++)
        {
            if (rows.TryGetValue(rowNumber, out var row))
            {
                collected.Add((rowNumber, row));
            }
        }

        if (collected.Count == 1 &&
            string.Equals(collected[0].Row.GetValueOrDefault("A", "").Trim(), NoneAddedYet, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return collected;
    }

    // ---------------------------------------------------------------- reading

    // ClosedXML raises different, sometimes undocumented exception types for a stream that isn't a
    // readable workbook at all (a corrupt file, or a zip that just isn't an .xlsx) - normalized to
    // a single InvalidDataException here so ImportAsync only ever has one exception type to treat
    // as "not a valid sheet."
    private static Dictionary<int, Dictionary<string, string>> ReadRows(Stream fileStream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("Workbook could not be read.", ex);
        }

        using (workbook)
        {
            if (!workbook.Worksheets.TryGetWorksheet(SheetName, out var worksheet))
            {
                throw new InvalidDataException($"Workbook has no '{SheetName}' worksheet.");
            }

            var rows = new Dictionary<int, Dictionary<string, string>>();
            foreach (var cell in worksheet.CellsUsed())
            {
                if (!rows.TryGetValue(cell.Address.RowNumber, out var row))
                {
                    row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    rows[cell.Address.RowNumber] = row;
                }

                row[cell.Address.ColumnLetter] = ReadCellText(cell);
            }

            return rows;
        }
    }

    // Returns the same shape the raw XML used to serialize, so every reader downstream (including
    // NormalizeExcelTime's day-fraction parsing) keeps working unchanged: a boolean cell as "1"/"0",
    // and a time-formatted cell as its day-fraction decimal rather than ClosedXML's own "HH:mm:ss"
    // rendering (that includes seconds - PullTimePattern only accepts "HH:mm").
    private static string ReadCellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.Blank => string.Empty,
        XLDataType.Boolean => cell.GetBoolean() ? "1" : "0",
        XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
        XLDataType.DateTime => cell.GetDateTime().TimeOfDay.TotalDays.ToString(CultureInfo.InvariantCulture),
        XLDataType.TimeSpan => cell.GetTimeSpan().TotalDays.ToString(CultureInfo.InvariantCulture),
        _ => cell.GetString()
    };
}
