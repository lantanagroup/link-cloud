using System.IO.Compression;
using System.Xml.Linq;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

// Rewrites the two identifying lines of a manual-upload import sheet - "Facility: ..." and
// "Vendor: ..." (rows 2 and 3 in both Epic_Import_Sheet.xlsx and Cerner_Import_Sheet.xlsx) - to
// name the actual downloading facility instead of whatever sample text was baked into the asset
// when it was authored. Everything else in the workbook (styles, the field rows, the reference
// vocabulary) is left byte-for-byte untouched.
//
// Deliberately its own small helper rather than a method on ManualUploadTemplateService: that
// service only ever reads an uploaded sheet, this only ever rewrites one about to be downloaded,
// and PackageZipDownloadService (the only caller) has no other reason to depend on the importer.
public static class ManualUploadTemplatePersonalizer
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

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
