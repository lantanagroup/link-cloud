<Query Kind="Statements">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
</Query>

/*
Analyzes a FHIR Bundle JSON file, collects insights about the resources it contains, and outputs various statistics about the bundle's structure and contents.

Steps:
1. **Default File Path Configuration**:
   - Sets a default file path `bundle.json` in the user's Downloads folder.
   - Prompts the user to provide a file path or use the default.

2. **File Validation**:
   - Checks if the provided file path exists and contains valid JSON.
   - If the file is not found or the JSON is invalid, an appropriate error is displayed, and the script terminates.

3. **FHIR Bundle Validation**:
   - Verifies that the JSON file represents a valid FHIR Bundle.
   - If the `resourceType` is not `"Bundle"`, the script exits with an error.

4. **Resource Analysis**:
   - Extracts the list of entries within the FHIR Bundle.
   - Iterates through each entry to analyze individual resources:
     - **Resource Type Counts**: Counts the occurrences of each `resourceType`.
     - **Resource Size Calculation**: Determines the largest resource by size (in KB) in the bundle.
     - **Missing Subject/Patient Reference**: Tracks the count of resources missing both `subject` and `patient` fields.

5. **Output Results**:
   - Displays the total number of resources in the bundle.
   - Outputs the largest resource size (in KB) found in the bundle.
   - Provides a breakdown of resource types with their counts.
   - Lists resources (grouped by type) missing `subject` or `patient` references.

The script leverages `JObject` from the Newtonsoft.Json library to parse and process the JSON structure and uses dictionaries for efficient grouping and counting of resource types and missing references. Results are presented in an ordered manner for better readability.
*/

// Prompt the user for the file path
string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "bundle.json");
var filePath = Util.ReadLine("Enter path to FHIR Bundle JSON file:", defaultPath);

if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
{
    $"File not found: {filePath}".Dump();
    return;
}

JObject bundle;
try
{
    bundle = JObject.Parse(File.ReadAllText(filePath));
}
catch (JsonReaderException)
{
    "Error: Invalid JSON format.".Dump();
    return;
}

if ((string)bundle["resourceType"] != "Bundle")
{
    "Error: JSON is not a FHIR Bundle.".Dump();
    return;
}

var entries = bundle["entry"]?.Children().ToList() ?? new List<JToken>();
var totalResources = entries.Count;

var resourceTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var missingSubjectPatientCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
double maxSizeKb = 0;

foreach (var entry in entries)
{
    var resource = entry["resource"];
    if (resource == null) continue;

    var resourceType = (string)resource["resourceType"] ?? "Unknown";
    resourceTypeCounts[resourceType] = resourceTypeCounts.GetValueOrDefault(resourceType, 0) + 1;

    var json = resource.ToString(Newtonsoft.Json.Formatting.None);
    var sizeKb = Encoding.UTF8.GetByteCount(json) / 1024.0;
    if (sizeKb > maxSizeKb) maxSizeKb = sizeKb;

	// Check for subject/patient reference
	var hasSubjectPatient = resource["subject"] != null || resource["patient"] != null;
	if (!hasSubjectPatient)
	{
		missingSubjectPatientCounts[resourceType] = missingSubjectPatientCounts.GetValueOrDefault(resourceType, 0) + 1;
	}
}

// Output the results
$"Total resources in bundle: {totalResources}".Dump();
$"Largest resource size: {maxSizeKb:F2} KB".Dump();

"Resource type breakdown:".Dump();
resourceTypeCounts
	.OrderBy(kvp => kvp.Key)
	.Dump("Resource Types");

"Resources missing subject/patient reference:".Dump();
missingSubjectPatientCounts
	.OrderBy(kvp => kvp.Key)
	.Dump("Missing Subject/Patient Reference");
