using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// A minimal single-sheet .xlsx reader/writer, hand-rolled rather than a library dependency: the
// workbook is one flat "label in column A, value in column B" sheet, which is all Open Packaging
// Conventions machinery a full Excel library exists for buys nothing here.
//
// Every value round-trips as text (t="inlineStr") — the sheet is a data-interchange form, not a
// spreadsheet anyone computes with, so a numeric field displaying as text in Excel costs nothing
// and keeps both the writer and the reader in this one file.
public sealed class ManualUploadTemplateService : IManualUploadTemplateService
{
    private const string SheetName = "Import";
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    private readonly IOnboardingReadService _readService;

    public ManualUploadTemplateService(IOnboardingReadService readService)
    {
        _readService = readService;
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var envelope = await _readService.GetAsync(cancellationToken);
        var draft = envelope.Draft ?? new FacilityDraftResponse();
        var fields = BuildFieldSpecs();

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            WriteEntry(archive, "_rels/.rels", BuildPackageRelsXml());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheetXml(fields, draft));
        }

        return stream.ToArray();
    }

    public Task<ImportResult> ImportAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> cellsByRef;
        try
        {
            cellsByRef = ReadCells(fileStream);
        }
        catch (InvalidDataException)
        {
            return Task.FromResult(InvalidFormatResult());
        }
        catch (System.Xml.XmlException)
        {
            return Task.FromResult(InvalidFormatResult());
        }

        var fields = BuildFieldSpecs();
        var errors = new List<ImportCellError>();
        var fieldsImported = 0;

        foreach (var field in fields)
        {
            if (!cellsByRef.TryGetValue(field.ValueCell, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            fieldsImported++;

            var messageKey = field.Validate(rawValue.Trim());
            if (messageKey is not null)
            {
                errors.Add(new ImportCellError
                {
                    Sheet = SheetName,
                    Cell = field.ValueCell,
                    MessageKey = messageKey
                });
            }
        }

        return Task.FromResult(new ImportResult
        {
            Accepted = errors.Count == 0,
            CellErrors = errors,
            FieldsImported = fieldsImported,
            TotalFields = fields.Count
        });
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
        TotalFields = BuildFieldSpecs().Count
    };

    // ---------------------------------------------------------------- field specs

    private sealed record FieldSpec(string Label, string LabelCell, string ValueCell, Func<FacilityDraftResponse, string?> Read, Func<string, string?> Validate);

    private static List<FieldSpec> BuildFieldSpecs()
    {
        var row = 1;
        var fields = new List<FieldSpec>();

        void Add(string label, Func<FacilityDraftResponse, string?> read, Func<string, string?>? validate = null)
        {
            row++;
            fields.Add(new FieldSpec(label, $"A{row}", $"B{row}", read, validate ?? (_ => null)));
        }

        Add("Facility Time Zone", d => d.FacilityInfo.TimeZone);
        Add("EHR Vendor", d => d.FacilityInfo.Vendor?.ToString(), ValidateVendor);
        Add("FHIR Server Base URL", d => d.Fhir.FhirServerBaseUrl, ValidateAbsoluteUrl);
        Add("Max Concurrent Requests", d => d.Fhir.MaxConcurrentRequests?.ToString(), ValidatePositiveInteger);
        Add("Max Retries", d => d.Fhir.MaxRetries?.ToString(), ValidatePositiveInteger);
        Add("Min Acquisition Pull Time", d => d.Fhir.MinAcquisitionPullTime);
        Add("Max Acquisition Pull Time", d => d.Fhir.MaxAcquisitionPullTime);
        Add("Acquisition Lag Duration", d => d.Fhir.LagDuration);
        Add("Census Acquisition Frequency", d => d.Census.AcquisitionFrequency);
        Add("Cerner sFTP Host", d => d.Census.SftpHost);
        Add("Cerner sFTP Port", d => d.Census.SftpPort?.ToString(), ValidatePositiveInteger);
        Add("Cerner sFTP Remote Directory", d => d.Census.SftpRemoteDirectory);
        Add("Organization Identification Method", d => d.LocationOrg.Method, ValidateLocationMethod);
        Add("Custom FHIR Path", d => d.LocationOrg.CustomFhirPath);
        Add("Managing Organization Ids", d => JoinList(d.LocationOrg.ManagingOrganizationIds));
        Add("Location Type Codes", d => JoinList(d.LocationOrg.LocationTypeCodes));
        Add("Location Identifiers", d => JoinList(d.LocationOrg.LocationIdentifiers));

        return fields;
    }

    private static string? JoinList(IReadOnlyList<string> values) => values.Count == 0 ? null : string.Join(";", values);

    private static string? ValidateVendor(string value) =>
        Enum.TryParse<EhrVendor>(value, ignoreCase: true, out _)
            ? null
            : "onboarding:manualUpload.errors.invalidVendor";

    private static string? ValidateAbsoluteUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out _)
            ? null
            : "onboarding:manualUpload.errors.invalidUrl";

    private static string? ValidatePositiveInteger(string value) =>
        int.TryParse(value, out var parsed) && parsed >= 0
            ? null
            : "onboarding:manualUpload.errors.invalidNumber";

    private static readonly HashSet<string> LocationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "managing-org", "location-identifier", "location-type", "custom-fhir-path"
    };

    private static string? ValidateLocationMethod(string value) =>
        LocationMethods.Contains(value)
            ? null
            : "onboarding:manualUpload.errors.invalidLocationMethod";

    // ---------------------------------------------------------------- writing

    private static void WriteEntry(ZipArchive archive, string entryName, string xml)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xml);
    }

    private static string BuildContentTypesXml() =>
        new XDocument(
            new XDeclaration("1.0", "UTF-8", "standalone"),
            new XElement(ContentTypes + "Types",
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))
            )).ToString(SaveOptions.DisableFormatting);

    private static string BuildPackageRelsXml() =>
        new XDocument(
            new XDeclaration("1.0", "UTF-8", "standalone"),
            new XElement(Relationships + "Relationships",
                new XElement(Relationships + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml"))
            )).ToString(SaveOptions.DisableFormatting);

    private static string BuildWorkbookXml() =>
        new XDocument(
            new XDeclaration("1.0", "UTF-8", "standalone"),
            new XElement(Main + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
                new XElement(Main + "sheets",
                    new XElement(Main + "sheet",
                        new XAttribute("name", SheetName),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships") + "id", "rId1"))
                )
            )).ToString(SaveOptions.DisableFormatting);

    private static string BuildWorkbookRelsXml() =>
        new XDocument(
            new XDeclaration("1.0", "UTF-8", "standalone"),
            new XElement(Relationships + "Relationships",
                new XElement(Relationships + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml"))
            )).ToString(SaveOptions.DisableFormatting);

    private static string BuildSheetXml(List<FieldSpec> fields, FacilityDraftResponse draft)
    {
        var rows = new List<XElement>
        {
            new(Main + "row", new XAttribute("r", "1"),
                InlineCell("A1", "Field"), InlineCell("B1", "Value"))
        };

        foreach (var field in fields)
        {
            var rowNumber = field.LabelCell[1..];
            var value = field.Read(draft) ?? string.Empty;
            rows.Add(new XElement(Main + "row", new XAttribute("r", rowNumber),
                InlineCell(field.LabelCell, field.Label),
                InlineCell(field.ValueCell, value)));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "standalone"),
            new XElement(Main + "worksheet",
                new XElement(Main + "sheetData", rows)
            )).ToString(SaveOptions.DisableFormatting);
    }

    private static XElement InlineCell(string reference, string text) =>
        new(Main + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"),
            new XElement(Main + "is", new XElement(Main + "t", text)));

    // ---------------------------------------------------------------- reading

    private static Dictionary<string, string> ReadCells(Stream fileStream)
    {
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        var sharedStrings = ReadSharedStrings(archive);

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidDataException("Workbook has no first worksheet.");

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);

        var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cellElement in document.Descendants(Main + "c"))
        {
            var reference = (string?)cellElement.Attribute("r");
            if (reference is null)
            {
                continue;
            }

            cells[reference] = ReadCellText(cellElement, sharedStrings);
        }

        return cells;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(Main + "si")
            .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value)))
            .ToList();
    }

    private static string ReadCellText(XElement cellElement, List<string> sharedStrings)
    {
        var type = (string?)cellElement.Attribute("t");

        if (type == "inlineStr")
        {
            return string.Concat(cellElement.Descendants(Main + "t").Select(t => t.Value));
        }

        var value = cellElement.Element(Main + "v")?.Value;
        if (value is null)
        {
            return string.Empty;
        }

        if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedIndex];
        }

        return value;
    }
}
