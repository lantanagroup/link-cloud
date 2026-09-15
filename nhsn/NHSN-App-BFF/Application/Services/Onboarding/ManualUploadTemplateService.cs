using System.IO.Compression;
using System.Xml.Linq;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// A minimal single-sheet .xlsx reader, hand-rolled rather than a library dependency: the
// workbook is one flat "label in column A, value in column B" sheet, which is all Open Packaging
// Conventions machinery a full Excel library exists for buys nothing here.
public sealed class ManualUploadTemplateService : IManualUploadTemplateService
{
    private const string SheetName = "Import";
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

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

    // Label documents which onboarding field a row maps to; the sheet no longer needs it, but a
    // bare list of value cells and validators would be unreadable.
    private sealed record FieldSpec(string Label, string ValueCell, Func<string, string?> Validate);

    private static List<FieldSpec> BuildFieldSpecs()
    {
        var row = 1;
        var fields = new List<FieldSpec>();

        void Add(string label, Func<string, string?>? validate = null)
        {
            row++;
            fields.Add(new FieldSpec(label, $"B{row}", validate ?? (_ => null)));
        }

        Add("Facility Time Zone");
        Add("EHR Vendor", ValidateVendor);
        Add("FHIR Server Base URL", ValidateAbsoluteUrl);
        Add("Max Concurrent Requests", ValidatePositiveInteger);
        Add("Max Retries", ValidatePositiveInteger);
        Add("Min Acquisition Pull Time");
        Add("Max Acquisition Pull Time");
        Add("Acquisition Lag Duration");
        Add("Census Acquisition Frequency");
        Add("Cerner sFTP Host");
        Add("Cerner sFTP Port", ValidatePositiveInteger);
        Add("Cerner sFTP Remote Directory");
        Add("Organization Identification Method", ValidateLocationMethod);
        Add("Custom FHIR Path");
        Add("Managing Organization Ids");
        Add("Location Types");
        Add("Location Identifiers");

        return fields;
    }

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
