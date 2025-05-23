<Query Kind="Program">
  <Namespace>System.Text.Json</Namespace>
</Query>

void Main()
{
	string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "validation-response.json");

	// Prompt for file path
	string filePath = Util.ReadLine("Please enter the path to the JSON file:", defaultPath);

	if (string.IsNullOrEmpty(filePath))
	{
		Console.WriteLine("No file path provided.");
		return;
	}

	if (!File.Exists(filePath))
	{
		Console.WriteLine($"File not found: {filePath}");
		return;
	}

	try
	{
		// Read and parse the JSON file
		string jsonContent = File.ReadAllText(filePath);
		var validationResults = JsonSerializer.Deserialize<List<ValidationResult>>(jsonContent);

		if (validationResults == null || !validationResults.Any())
		{
			Console.WriteLine("No validation results found in the JSON file.");
			return;
		}

		// Find messages starting with "CodeSystem is unknown"
		var unknownCodeSystems = validationResults
			.Where(r => r.message?.StartsWith("CodeSystem is unknown", StringComparison.OrdinalIgnoreCase) == true)
			.Select(r => ExtractCodeSystemUrl(r.message))
			.Where(url => url != null)
			.Distinct()
			.ToList();

		// Output results
		if (unknownCodeSystems.Any())
		{
			Console.WriteLine("\nFound the following unique unknown CodeSystem URLs:");
			foreach (var url in unknownCodeSystems)
			{
				Console.WriteLine(url);
			}
			Console.WriteLine($"\nTotal unique CodeSystem URLs found: {unknownCodeSystems.Count}");
		}
		else
		{
			Console.WriteLine("No messages containing unknown CodeSystems were found.");
		}
	}
	catch (JsonException ex)
	{
		Console.WriteLine($"Error parsing JSON file: {ex.Message}");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Unexpected error: {ex.Message}");
	}
}

// Define the data structure
public class ValidationResult
{
	public string severity { get; set; }
	public string code { get; set; }
	public string message { get; set; }
	public string location { get; set; }
	public string expression { get; set; }
}

// Helper method to extract CodeSystem URL from message
private string ExtractCodeSystemUrl(string message)
{
	if (string.IsNullOrEmpty(message)) return null;

	// Find the position after "CodeSystem is unknown and can't be validated: "
	int startIndex = message.IndexOf(":", StringComparison.OrdinalIgnoreCase);
	if (startIndex == -1) return null;

	startIndex++; // Move past the colon

	// Find the position of " for " which indicates the end of the URL
	int endIndex = message.IndexOf(" for ", startIndex, StringComparison.OrdinalIgnoreCase);
	if (endIndex == -1) return null;

	// Extract and trim the URL
	return message.Substring(startIndex, endIndex - startIndex).Trim();
}