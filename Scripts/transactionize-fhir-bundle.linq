<Query Kind="Statements">
  <Namespace>System.Text.Json.Nodes</Namespace>
  <Namespace>System.Text.Json</Namespace>
</Query>

/*
    This script processes all FHIR JSON bundle files in a specified directory. It performs the following tasks
    for each bundle to ensure compliance with specific formatting and structural requirements:

    1. Sets the "type" of the bundle to "batch".
    2. Removes unwanted fields from the bundle, including:
        - "link"
        - "meta"
        - "id"
        - "total"
    3. Iterates over all the entries in the bundle and:
        - Removes "fullUrl" and "search" fields from each entry.
        - Removes the "meta" field from the "resource" object of each entry.
        - Adds a "request" object to each entry with PUT method and a URL in the format "[ResourceType]/[id]".

    Any files that cannot be processed or entries with missing "resourceType" or "id" fields are skipped, and a 
    message is displayed in the output window.

    The modified bundles are saved back to their original files in the specified directory.

    Usage Notes:
    - Update the 'BundleDirectory' constant to point to the directory containing your FHIR JSON bundle files.
    - Use LINQPad's "Dump()" method to review processing information and output errors in the LINQPad Result pane.
*/

// Specify the directory containing the FHIR JSON bundles
string BundleDirectory = Util.ReadLine("Directory:", @"D:\Code\link-cloud\Tests\E2ETests\fhir_server_data");
string BundleType = Util.ReadLine("Bundle Type:", "batch");

// Loop through the JSON files in the directory
foreach (var file in Directory.GetFiles(BundleDirectory, "*.json"))
{
	$"Processing {Path.GetFileName(file)}".Dump(); // LINQPad .Dump() method for output

	try
	{
		var jsonText = File.ReadAllText(file);
		var root = JsonNode.Parse(jsonText)?.AsObject();
		if (root == null)
		{
			$"Could not parse JSON in {file}".Dump();
			continue;
		}

		// 1. Set type to "batch"
		root["type"] = BundleType;

		// 5. Remove bundle.link, bundle.meta, and bundle.id
		root.Remove("link");
		root.Remove("meta");
		root.Remove("id");
		root.Remove("total");

		var entries = root["entry"]?.AsArray();
		if (entries == null)
		{
			$"No entries in {file}".Dump();
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
				$"Skipping entry with missing resourceType or id in {file}".Dump();
			}
		}

		// Save modified file
		File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
		Console.WriteLine("Done!");
	}
	catch (Exception ex)
	{
		$"Error processing {file}: {ex.Message}".Dump(); // Exception handling for errors
	}
}