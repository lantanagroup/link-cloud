namespace LantanaGroup.Link.Tests.E2ETests;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class FhirBundleFixer
{
    private const string BundleDirectory = @"D:\Code\link-cloud\Tests\E2ETests\data";

    [TestMethod]
    [Ignore]
    public void FixFhirJsonBundlesInDirectory()
    {
        foreach (var file in Directory.GetFiles(BundleDirectory, "*.json"))
        {
            Console.WriteLine($"Processing {Path.GetFileName(file)}");

            var jsonText = File.ReadAllText(file);
            var root = JsonNode.Parse(jsonText).AsObject();

            // 1. Set type to "batch"
            root["type"] = "batch";

            // 5. Remove bundle.link, bundle.meta, and bundle.id
            root.Remove("link");
            root.Remove("meta");
            root.Remove("id");
            root.Remove("total");

            var entries = root["entry"]?.AsArray();
            if (entries == null)
            {
                Console.WriteLine($"No entries in {file}");
                continue;
            }

            foreach (var entry in entries)
            {
                var entryObj = entry.AsObject();

                // 4. Remove fullUrl and search
                entryObj.Remove("fullUrl");
                entryObj.Remove("search");

                // 3. Remove resource.meta
                var resource = entryObj["resource"]?.AsObject();
                resource?.Remove("meta");

                // 2. Add request: PUT + [ResourceType]/[id]
                var resourceType = resource?["resourceType"]?.ToString();
                var id = resource?["id"]?.ToString();

                if (!string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(id))
                {
                    entryObj["request"] = new JsonObject
                    {
                        ["method"] = "PUT",
                        ["url"] = $"{resourceType}/{id}"
                    };
                }
                else
                {
                    Console.WriteLine($"Skipping entry with missing resourceType or id in {file}");
                }
            }

            // Save modified file
            File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
