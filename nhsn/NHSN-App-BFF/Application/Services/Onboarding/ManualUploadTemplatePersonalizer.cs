using System.IO.Compression;
using System.Xml.Linq;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Rewrites the two identifying lines of a manual-upload import sheet - "Facility: ..." and
// "Vendor: ..." (rows 2 and 3 in both Epic_Import_Sheet.xlsx and Cerner_Import_Sheet.xlsx) - to
// name the actual downloading facility instead of whatever sample text was baked into the asset
// when it was authored. ReplaceReferenceSheet does the same for the "HSLOC Reference" /
// "Encounter Reference" tabs, swapping their baked-in data rows for the live reference vocabulary
// so the sheet a facility picks codes from matches what the import validates against. Everything
// else in the workbook (styles, header rows, column widths, the field rows) is left untouched.
//
// Deliberately its own small helper rather than a method on ManualUploadTemplateService: that
// service only ever reads an uploaded sheet, this only ever rewrites one about to be downloaded,
// and PackageZipDownloadService (the only caller) has no other reason to depend on the importer.
public static class ManualUploadTemplatePersonalizer
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Returns a copy of <paramref name="workbookBytes"/> whose worksheet named
    /// <paramref name="sheetName"/> keeps its header row (row 1) but has every other row replaced by
    /// <paramref name="rows"/>, one cell per value from column A. Values are written as inline
    /// strings so the shared-string table never needs touching. Falls back to returning the workbook
    /// unchanged when there's nothing to write or the sheet can't be found - same "never fail the
    /// download" rule as <see cref="Personalize"/>, leaving the template's own baked-in rows in place.
    /// </summary>
    public static byte[] ReplaceReferenceSheet(byte[] workbookBytes, string sheetName, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
        {
            return workbookBytes;
        }

        using var stream = new MemoryStream();
        stream.Write(workbookBytes, 0, workbookBytes.Length);
        stream.Position = 0;

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entryName = FindWorksheetEntryName(archive, sheetName);
            var entry = entryName is null ? null : archive.GetEntry(entryName);
            if (entry is null)
            {
                return workbookBytes;
            }

            XDocument document;
            using (var entryStream = entry.Open())
            {
                document = XDocument.Load(entryStream);
            }

            var sheetData = document.Root?.Element(Main + "sheetData");
            if (sheetData is null)
            {
                return workbookBytes;
            }

            var columnCount = rows.Max(row => row.Count);
            sheetData.Elements(Main + "row")
                .Where(row => (string?)row.Attribute("r") != "1")
                .Remove();

            for (var index = 0; index < rows.Count; index++)
            {
                var rowNumber = index + 2;
                var row = new XElement(Main + "row",
                    new XAttribute("r", rowNumber),
                    new XAttribute("spans", $"1:{columnCount}"));
                for (var column = 0; column < rows[index].Count; column++)
                {
                    row.Add(new XElement(Main + "c",
                        new XAttribute("r", $"{ColumnLetter(column)}{rowNumber}"),
                        new XAttribute("t", "inlineStr"),
                        new XElement(Main + "is", new XElement(Main + "t", XmlSafe(rows[index][column])))));
                }
                sheetData.Add(row);
            }

            document.Root!.Element(Main + "dimension")?.SetAttributeValue("ref", $"A1:{ColumnLetter(columnCount - 1)}{rows.Count + 1}");

            entry.Delete();
            var rewritten = archive.CreateEntry(entryName!, CompressionLevel.Optimal);
            using var writer = rewritten.Open();
            document.Save(writer);
        }

        return stream.ToArray();
    }

    // Resolves a tab name to its part (e.g. "xl/worksheets/sheet2.xml") through workbook.xml and its
    // relationships, rather than assuming a fixed sheetN - the tabs are found by what they're called,
    // same as every other lookup against these templates.
    private static string? FindWorksheetEntryName(ZipArchive archive, string sheetName)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relsEntry is null)
        {
            return null;
        }

        XDocument workbook;
        using (var workbookStream = workbookEntry.Open())
        {
            workbook = XDocument.Load(workbookStream);
        }
        var relationshipId = workbook.Descendants(Main + "sheet")
            .FirstOrDefault(sheet => string.Equals((string?)sheet.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase))
            ?.Attribute(OfficeRelationships + "id")?.Value;
        if (relationshipId is null)
        {
            return null;
        }

        XDocument rels;
        using (var relsStream = relsEntry.Open())
        {
            rels = XDocument.Load(relsStream);
        }
        var target = rels.Descendants(PackageRelationships + "Relationship")
            .FirstOrDefault(rel => (string?)rel.Attribute("Id") == relationshipId)
            ?.Attribute("Target")?.Value;
        if (target is null)
        {
            return null;
        }

        return target.StartsWith('/') ? target.TrimStart('/') : $"xl/{target}";
    }

    private static string ColumnLetter(int zeroBasedIndex) => ((char)('A' + zeroBasedIndex)).ToString();

    // Reference text comes from other services; a stray control character would make the part
    // unreadable to Excel, so drop anything XML can't carry.
    private static string XmlSafe(string value) =>
        new(value.Trim().Where(System.Xml.XmlConvert.IsXmlChar).ToArray());

    /// <summary>
    /// Returns a copy of <paramref name="templateBytes"/> with its "Facility:" and "Vendor:" lines
    /// replaced. Falls back to returning the template unchanged if the sheet isn't shaped the way
    /// every deployed template is (missing worksheet, neither line present) - a download should
    /// never fail outright just because personalizing it couldn't find where to write.
    /// </summary>
    public static byte[] Personalize(byte[] templateBytes, string facilityLine, string vendorLine)
    {
        using var stream = new MemoryStream();
        stream.Write(templateBytes, 0, templateBytes.Length);
        stream.Position = 0;

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml");
            if (entry is null)
            {
                return templateBytes;
            }

            XDocument document;
            using (var entryStream = entry.Open())
            {
                document = XDocument.Load(entryStream);
            }

            var replacedFacility = ReplaceLine(document, "Facility:", facilityLine);
            var replacedVendor = ReplaceLine(document, "Vendor:", vendorLine);
            if (!replacedFacility && !replacedVendor)
            {
                return templateBytes;
            }

            entry.Delete();
            var rewritten = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
            using var writer = rewritten.Open();
            document.Save(writer);
        }

        return stream.ToArray();
    }

    // Finds the first inline-string cell whose text starts with `prefix` (e.g. "Facility:") and
    // replaces its whole text with `newText`, wherever it lands in the sheet - matching how every
    // other lookup in ManualUploadTemplateService locates content by what it says, not where it
    // sits, so this survives the row moving if the template is ever re-laid-out.
    private static bool ReplaceLine(XDocument document, string prefix, string newText)
    {
        foreach (var cell in document.Descendants(Main + "c"))
        {
            var textElement = cell.Descendants(Main + "t").FirstOrDefault();
            if (textElement is null || !textElement.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            textElement.Value = newText;
            return true;
        }
        return false;
    }
}
