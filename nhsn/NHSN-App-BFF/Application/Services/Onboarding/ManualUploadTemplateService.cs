using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

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
    private readonly IFacilityGateway _facilityGateway;
    private readonly INhsnUserContext _userContext;
    private readonly IReferenceDataService _referenceDataService;

    public ManualUploadTemplateService(
        IOnboardingWriteService writeService,
        IFacilityGateway facilityGateway,
        INhsnUserContext userContext,
        IReferenceDataService referenceDataService)
    {
        _writeService = writeService;
        _facilityGateway = facilityGateway;
        _userContext = userContext;
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

        // The sheet's own "Facility: ... (ID)" line (row 2) names which facility it was generated
        // for. Checked before the vendor line: a sheet downloaded for facility A (or hand-edited to
        // claim to be one) has no business being applied to facility B's config, regardless of
        // whether the vendors happen to match - the facility id is the one thing on the sheet
        // that's supposed to be unique per download.
        var facilityId = _userContext.RequireFacilityId();
        var sheetFacilityId = ParseSheetFacilityId(rows);
        if (sheetFacilityId is not null && !string.Equals(sheetFacilityId, facilityId, StringComparison.OrdinalIgnoreCase))
        {
            return FacilityMismatchResult();
        }

        // The sheet's own "Vendor: Epic"/"Vendor: Cerner" line (row 3 in both templates, but
        // matched by content rather than position - same reasoning as every other lookup in this
        // file) names which vendor it was generated for. Uploading the other vendor's sheet would
        // otherwise silently parse - the two templates share every field key - and write values a
        // facility never actually entered (an Epic facility has no sftpHost/sftpPassword rows to
        // fill in, so a Cerner sheet's real SFTP credentials would flow straight into an Epic
        // facility's config). Only checked once the facility has actually chosen a vendor; nothing
        // to compare against otherwise.
        var facility = await _facilityGateway.GetAsync(facilityId, cancellationToken);
        if (facility?.Vendor is { } configuredVendor)
        {
            var sheetVendor = ParseSheetVendor(rows);
            if (sheetVendor is not null && !string.Equals(sheetVendor, configuredVendor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return VendorMismatchResult();
            }
        }

        var errors = new List<ImportCellError>();
        var values = ReadScalarFields(rows, out var rowNumberByKey, errors);
        ValidateCrossFields(values, rowNumberByKey, rows, errors);

        var fields = new ImportedFields
        {
            Fhir = BuildFhir(values),
            Census = BuildCensus(values),
            LocationOrg = BuildLocationOrg(values, rows),
            Hsloc = await BuildHslocAsync(rows, cancellationToken),
            Encounter = await BuildEncounterAsync(rows, cancellationToken)
        };

        var hslocImported = fields.Hsloc?.Mappings is {Count: > 0};
        var encounterImported = fields.Encounter?.Mappings is {Count: > 0};

        var totalFields = rows.Values.Count(row => row.TryGetValue("A", out var key) && ScalarFieldKeys.Contains(key))
            + 1 // HSLOC Location Mapping
            + 1; // Encounter Mapping

        // Only a fully valid sheet gets saved - a cell error rejects the whole import (Accepted =
        // false), so nothing here is ever written half-validated. Accepted is captured before a
        // save is even attempted, and stays true afterwards regardless of what the save reports:
        // it means "the sheet itself was well-formed," not "everything on it ended up saved." A
        // section can still fail once a real cross-service precondition or downstream validator -
        // never checkable from the sheet alone - rejects it; that's reported as a cell error too,
        // just added after the fact, so the facility sees why a field came back blank instead of
        // guessing, without losing the sections that DID save.
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
            FieldsImported = values.Count + (hslocImported ? 1 : 0) + (encounterImported ? 1 : 0),
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
        var merged = original
            .Select(row => savedByCode.TryGetValue(row.SourceCode, out var savedRow)
                ? new ImportedHslocMapping { SourceCode = savedRow.SourceCode, SourceDisplay = savedRow.SourceDisplay, HslocCode = savedRow.HslocCode }
                : row)
            .ToList();
        return merged.Count > 0 ? merged : null;
    }

    // Same reasoning as MergeHslocMappings: a row with a blank EncounterType (local system/code
    // entered, reference columns didn't resolve) never round-trips through Normalization's Code Map
    // operation on its own - EncounterMappingService.BuildCodeSystemMaps skips anything it can't
    // split on '|'. Restricted to rows `original` (this import) actually named: `saved` is the
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
        var merged = original
            .Select(row => savedByKey.TryGetValue((row.System.ToUpperInvariant(), row.Code.ToUpperInvariant()), out var savedRow)
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

    private static ImportResult VendorMismatchResult() => new()
    {
        Accepted = false,
        CellErrors =
        [
            new ImportCellError
            {
                Sheet = SheetName,
                Cell = "A3",
                MessageKey = "onboarding:manualUpload.errors.vendorMismatch"
            }
        ],
        FieldsImported = 0,
        TotalFields = 0
    };

    private static ImportResult FacilityMismatchResult() => new()
    {
        Accepted = false,
        CellErrors =
        [
            new ImportCellError
            {
                Sheet = SheetName,
                Cell = "A2",
                MessageKey = "onboarding:manualUpload.errors.facilityMismatch"
            }
        ],
        FieldsImported = 0,
        TotalFields = 0
    };

    private static readonly Regex VendorLinePattern = new(@"^Vendor:\s*(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches PackageZipDownloadService's own "Facility: {name} ({id})" format (see
    // ManualUploadTemplatePersonalizer) - the id in parentheses at the end of the line, not the
    // display name before it, since the name alone isn't guaranteed unique and may contain
    // parentheses of its own.
    private static readonly Regex FacilityLinePattern = new(@"^Facility:\s*.*\(([^()]+)\)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Scans every row rather than assuming row 3, matching how every other lookup in this file
    // locates content by what it says instead of where it sits.
    private static string? ParseSheetVendor(Dictionary<int, Dictionary<string, string>> rows)
    {
        foreach (var row in rows.Values)
        {
            if (row.TryGetValue("A", out var text))
            {
                var match = VendorLinePattern.Match(text.Trim());
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }
        return null;
    }

    // Same approach as ParseSheetVendor, against row 2 instead of row 3.
    private static string? ParseSheetFacilityId(Dictionary<int, Dictionary<string, string>> rows)
    {
        foreach (var row in rows.Values)
        {
            if (row.TryGetValue("A", out var text))
            {
                var match = FacilityLinePattern.Match(text.Trim());
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }
        return null;
    }

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

        var censusHours = ParseInt(values, "censusFreqHours");
        var censusMinutes = ParseInt(values, "censusFreqMinutes");
        // Fires as soon as EITHER is present, defaulting the other to 0 the same way the lag
        // check above does - a facility that fills in only "Hours" (leaving "Minutes" blank,
        // its default) was previously invisible to this check entirely, since it used to require
        // both fields non-null before running at all.
        if ((censusHours is not null || censusMinutes is not null) &&
            !FieldValidationRules.IsValidCensusFrequency(censusHours ?? 0, censusMinutes ?? 0))
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

    // A row's HSLOC Reference Code is only ever something a facility copied off the HSLOC reference
    // table (there is no other source for that column), so a code that doesn't resolve there is
    // typo'd or stale - but the row itself still names a real location identifier the facility wants
    // mapped, so it's kept rather than dropped. HslocCode comes back blank instead, which HslocStep's
    // own mapping-row <select> already renders as its "Select an HSLOC code" placeholder (the same
    // state a brand-new row starts in online) - the facility sees every location they entered and
    // just has to pick the right code for the ones that didn't resolve, instead of quietly losing
    // rows to a bad or outdated code. Also enforces "one location identifier maps to one HSLOC code":
    // Excel allows a facility to paste a row twice or reuse a source code by mistake, and
    // HslocMappingService.SaveAsync already collapses duplicate source codes down to whichever one
    // happened to sort first - deduping here, keeping the sheet's first occurrence, makes that
    // outcome the sheet's own order rather than an implementation detail two layers away.
    private async Task<ImportedHsloc?> BuildHslocAsync(
        Dictionary<int, Dictionary<string, string>> rows,
        CancellationToken cancellationToken)
    {
        var rawRows = ReadTableRows(rows, "HSLOC Location Mapping");
        if (rawRows.Count == 0)
        {
            return null;
        }

        var referenceCodes = await _referenceDataService.GetHslocCodesAsync(cancellationToken);
        var referenceByCode = referenceCodes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var seenSourceCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings = new List<ImportedHslocMapping>();
        foreach (var (_, row) in rawRows)
        {
            var sourceCode = row.GetValueOrDefault("B", "").Trim();
            var hslocCode = row.GetValueOrDefault("C", "").Trim();
            var sourceDisplay = row.GetValueOrDefault("A", "").Trim();
            if (sourceCode.Length == 0)
            {
                continue;
            }
            if (!seenSourceCodes.Add(sourceCode))
            {
                continue;
            }

            // A code that doesn't resolve against the reference table (typo'd, stale, or just
            // never filled in) is left blank rather than raising a cell error - HslocCode comes
            // back "" either way, and the facility picks the right one on HslocStep itself, same
            // as a freshly added row started online. Never rejects the import over this.
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

        return mappings.Count > 0 ? new ImportedHsloc { Mappings = mappings } : null;
    }

    // Same "resolve against the real reference table, or leave it for the facility to pick" rule as
    // HSLOC, against Terminology's CPT/SNOMED encounter-type codes instead of Normalization's HSLOC
    // list. A row whose reference columns don't resolve still names a real local system/code the
    // facility wants mapped, so it's kept with a blank EncounterType/Display - EncounterStep's own
    // target-code picker already renders that as an empty combobox ready for a manual pick (the same
    // state a freshly added row starts in online), and its own validate.ts already flags a
    // local-value-with-no-target row without blocking Continue over it. The resolved System/Code/
    // Display come from the reference row rather than the sheet's own text so a facility's casing/
    // whitespace on the reference columns never diverges from what Link's reference table holds.
    private async Task<ImportedEncounter?> BuildEncounterAsync(
        Dictionary<int, Dictionary<string, string>> rows,
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
        foreach (var (_, row) in rawRows)
        {
            var system = row.GetValueOrDefault("A", "").Trim();
            var code = row.GetValueOrDefault("B", "").Trim();
            var referenceSystem = row.GetValueOrDefault("C", "").Trim();
            var referenceCode = row.GetValueOrDefault("D", "").Trim();
            if (system.Length == 0 || code.Length == 0)
            {
                continue;
            }

            // A reference System/Code that doesn't resolve (typo'd, stale, or never filled in) is
            // left blank rather than raising a cell error - EncounterType comes back "" either way,
            // and the facility picks the right target on EncounterStep itself, same as a freshly
            // added row started online. Never rejects the import over this.
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
    private static List<(int RowNumber, Dictionary<string, string> Row)> ReadTableRows(
        Dictionary<int, Dictionary<string, string>> rows,
        string sectionHeader)
    {
        var headerRow = rows
            .Where(kv => kv.Value.TryGetValue("A", out var text) && string.Equals(text, sectionHeader, StringComparison.Ordinal))
            .Select(kv => (int?)kv.Key)
            .FirstOrDefault();

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
